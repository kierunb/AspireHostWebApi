using System.Collections.Concurrent;
using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WebApiMediatorCQRS.ApiModels;
using WebApiMediatorCQRS.Behaviors;
using WebApiMediatorCQRS.Commands;
using WebApiMediatorCQRS.Endpoints;
using WebApiMediatorCQRS.Queries;

namespace WebApiMediatorCQRS.Tests.Tests;

public class CachingBehaviorTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static readonly ProductResponse Product = new(1, "Tea", null, null, null, 10, 1, 0, 0, false);
    private static readonly SupplierResponse Supplier = new(1, "Supplier", null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public async Task AllReadQueriesCacheSuccessfulResults()
    {
        using var services = CreateServices();
        await AssertCached(services, new GetAllCustomersQuery(), (IEnumerable<GetAllCustomersQueryResponse>)[new() { CustomerId = "ALFKI" }]);
        await AssertCached(services, new GetCustomerByIdQuery("ALFKI"), new GetCustomerByIdQueryResponse { CustomerId = "ALFKI" });
        await AssertCached(services, new GetAllProductsQuery(), (IReadOnlyList<ProductResponse>)[Product]);
        await AssertCached(services, new GetProductByIdQuery(1), Product);
        await AssertCached(services, new GetAllSuppliersQuery(), (IReadOnlyList<SupplierResponse>)[Supplier]);
        await AssertCached(services, new GetSupplierByIdQuery(1), Supplier);
    }

    [Fact]
    public async Task EmptySuccessfulListsAreCached()
    {
        using var services = CreateServices();
        await AssertCached(services, new GetAllProductsQuery(), (IReadOnlyList<ProductResponse>)[]);
    }

    [Fact]
    public async Task ConcurrentMissesExecuteHandlerOnce()
    {
        using var services = CreateServices();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var query = new GetProductByIdQuery(1);
        async Task<ProductResponse?> Handler(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return Product;
        }

        var first = Execute(services, query, Handler);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        var waiters = Enumerable.Range(0, 10).Select(_ => Execute(services, query, Handler)).ToArray();
        release.SetResult();
        var results = await Task.WhenAll(waiters.Prepend(first)).WaitAsync(TimeSpan.FromSeconds(5), TestToken);

        Assert.All(results, result => Assert.Equal(Product, result));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExceptionsAndValidationFailuresAreNotCached(bool validationFailure)
    {
        using var services = CreateServices();
        var calls = 0;
        var error = validationFailure ? (Exception)new ValidationException("Invalid query") : new InvalidOperationException("Query failed");
        Task<ProductResponse?> Handler(CancellationToken token)
        {
            calls++;
            return Task.FromException<ProductResponse?>(error);
        }

        for (var i = 0; i < 2; i++)
        {
            var actual = await Record.ExceptionAsync(() => Execute(services, new GetProductByIdQuery(1), Handler));
            Assert.Same(error, actual);
        }
        Assert.Equal(2, calls);
        await AssertCached(services, new GetProductByIdQuery(1), Product);
    }

    [Fact]
    public async Task NullAndUnsuccessfulResultsAreNotCached()
    {
        using var services = CreateServices();
        var calls = 0;
        Task<ProductResponse?> Missing(CancellationToken token)
        {
            calls++;
            return Task.FromResult<ProductResponse?>(null);
        }
        Assert.Null(await Execute(services, new GetProductByIdQuery(1), Missing));
        Assert.Null(await Execute(services, new GetProductByIdQuery(1), Missing));
        Assert.Equal(2, calls);

        var unsuccessfulCalls = 0;
        Task<ProbeResponse> Unsuccessful(CancellationToken token)
        {
            unsuccessfulCalls++;
            return Task.FromResult(new ProbeResponse(false));
        }
        Assert.False((await Execute(services, new ProbeQuery(), Unsuccessful)).Success);
        Assert.False((await Execute(services, new ProbeQuery(), Unsuccessful)).Success);
        Assert.Equal(2, unsuccessfulCalls);
    }

    [Fact]
    public async Task InvalidQueriesBypassCache()
    {
        using var services = CreateServices();
        await AssertNotCached(services, new GetProductByIdQuery(0), Product);
        await AssertNotCached(services, new GetSupplierByIdQuery(-1), Supplier);
        await AssertNotCached(services, new GetCustomerByIdQuery("bad"), new GetCustomerByIdQueryResponse());
    }

    [Fact]
    public async Task NonCacheableRequestsForwardCancellationAndExecuteEveryTime()
    {
        using var services = CreateServices();
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Task<PingCommandResponse> Handler(CancellationToken token)
        {
            Assert.Equal(cancellation.Token, token);
            calls++;
            return Task.FromResult(new PingCommandResponse("hello"));
        }
        services.GetRequiredService<TestHandlers>().Callbacks[typeof(PingCommand)] = (RequestHandlerDelegate<PingCommandResponse>)Handler;
        for (var i = 0; i < 2; i++)
            Assert.Equal("hello", (await services.GetRequiredService<IMediator>().Send(new PingCommand(), cancellation.Token)).Message);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CancellationReachesFactoryAndDoesNotCacheResult()
    {
        using var services = CreateServices();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ProductResponse?> Handler(CancellationToken token)
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult(token);
            await Task.Delay(Timeout.Infinite, token);
            return Product;
        }
        var pending = Execute(services, new GetProductByIdQuery(1), Handler, cancellation.Token);
        Assert.True((await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestToken)).CanBeCanceled);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        await AssertCached(services, new GetProductByIdQuery(1), Product);
    }

    [Fact]
    public async Task CancellingFirstCallerKeepsSharedHandlerScopeAliveForWaiter()
    {
        using var services = CreateServices();
        using var firstCaller = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ScopeLifetime? handlerScope = null;
        services.GetRequiredService<TestHandlers>().OnHandle = scope => handlerScope = scope;
        var calls = 0;
        async Task<ProductResponse?> Handler(CancellationToken token)
        {
            calls++;
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Product;
        }
        var first = Execute(services, new GetProductByIdQuery(1), Handler, firstCaller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        var waiter = Execute(services, new GetProductByIdQuery(1), Handler);
        firstCaller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.NotNull(handlerScope);
        Assert.False(handlerScope.IsDisposed);
        release.SetResult();

        Assert.Equal(Product, await waiter.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.True(handlerScope.IsDisposed);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task MutationFencesOffOlderInFlightReads()
    {
        using var services = CreateServices();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = Product;
        var calls = 0;
        async Task<ProductResponse?> Handler(CancellationToken token)
        {
            var snapshot = current;
            if (++calls == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
            return snapshot;
        }
        var query = new GetProductByIdQuery(1);
        var oldRead = Execute(services, query, Handler);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestToken);
        current = Product with { ProductName = "Updated" };
        await services.GetRequiredService<IMediator>().Send(
            new UpdateProductCommand(1, "Updated", null, null, null, null, null, null, null, false), TestToken);

        var newRead = Execute(services, query, Handler);
        Assert.Equal(current, await newRead.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        release.SetResult();
        Assert.Equal(Product, await oldRead.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        Assert.Equal(current, await Execute(services, query, Handler));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task MutationResponsesAreNeverCached()
    {
        using var services = CreateServices();
        var calls = 0;
        services.GetRequiredService<TestHandlers>().Callbacks[typeof(DeleteProductCommand)] =
            (RequestHandlerDelegate<ProductMutationStatus>)(_ =>
            {
                calls++;
                return Task.FromResult(ProductMutationStatus.Success);
            });
        var mediator = services.GetRequiredService<IMediator>();
        for (var i = 0; i < 2; i++)
            Assert.Equal(ProductMutationStatus.Success, await mediator.Send(new DeleteProductCommand(1), TestToken));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CacheLogsContainOperationAndQueryTypeWithoutKeysOrResponseData()
    {
        var logger = new RecordingLogger<CachingBehavior<GetCustomerByIdQuery, GetCustomerByIdQueryResponse?>>();
        using var services = CreateServices(configure: registrations =>
            registrations.AddSingleton<ILogger<CachingBehavior<GetCustomerByIdQuery, GetCustomerByIdQueryResponse?>>>(logger));
        await AssertCached(services, new GetCustomerByIdQuery("ALFKI"),
            new GetCustomerByIdQueryResponse { CustomerId = "ALFKI", ContactName = "Private contact" });

        Assert.Contains(logger.Entries, entry => Equals(entry["{OriginalFormat}"], "Cache miss for {QueryType}"));
        Assert.All(logger.Entries, entry => Assert.Equal(nameof(GetCustomerByIdQuery), entry["QueryType"]));
        var logValues = string.Join(" ", logger.Entries.SelectMany(entry => entry.Values));
        Assert.DoesNotContain("ALFKI", logValues);
        Assert.DoesNotContain("Private contact", logValues);
        Assert.DoesNotContain(new GetCustomerByIdQuery("ALFKI").CacheKey, logValues);
    }

    [Fact]
    public async Task ExpirationUsesConfigurationAndQueryOverride()
    {
        var clock = new CacheClock();
        using var services = CreateServices(clock);
        var calls = 0;
        Task<ProbeResponse> Handler(CancellationToken token)
        {
            calls++;
            return Task.FromResult(new ProbeResponse(true));
        }
        var query = new ProbeQuery();
        await Execute(services, query, Handler);
        clock.UtcNow += TimeSpan.FromSeconds(59);
        await Execute(services, query, Handler);
        Assert.Equal(1, calls);
        clock.UtcNow += TimeSpan.FromSeconds(2);
        await Execute(services, query, Handler);
        Assert.Equal(2, calls);

        var shortLivedQuery = new ProbeQuery(TimeSpan.FromSeconds(5));
        await services.GetRequiredService<HybridCache>().RemoveAsync(
            services.GetRequiredService<CacheInvalidationState>().GetKey(query.CacheKey, []), TestToken);
        await Execute(services, shortLivedQuery, Handler);
        clock.UtcNow += TimeSpan.FromSeconds(6);
        await Execute(services, shortLivedQuery, Handler);
        Assert.Equal(4, calls);
    }

    [Fact]
    public void KeysAreDeterministicParameterScopedAndCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var key = new GetProductByIdQuery(123).CacheKey;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(key, new GetProductByIdQuery(123).CacheKey);
            Assert.NotEqual(key, new GetProductByIdQuery(124).CacheKey);
            Assert.NotEqual(key, new GetSupplierByIdQuery(123).CacheKey);
            Assert.NotEqual(new GetAllProductsQuery().CacheKey, key);
            Assert.NotEqual(QueryCache.Key<ProbeQuery>("a:b", "c"), QueryCache.Key<ProbeQuery>("a", "b:c"));
            Assert.NotEqual(QueryCache.Key<ProbeQuery>((object?)null), QueryCache.Key<ProbeQuery>(""));
            Assert.NotEqual(new GetCustomerByIdQuery("ALFKI").CacheKey, new GetCustomerByIdQuery("alfki").CacheKey);
            Assert.DoesNotContain("ALFKI", new GetCustomerByIdQuery("ALFKI").CacheKey);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task SuccessfulMutationsInvalidateListsAndDetailsButNotOtherResources()
    {
        using var services = CreateServices();
        await AssertInvalidates(services, new CreateProductCommand("Tea", null, null, null, null, null, null, null, false), new ProductMutationResult(ProductMutationStatus.Success), ProductMutationResultFailure(), QueryCache.Products);
        await AssertInvalidates(services, new UpdateProductCommand(1, "Tea", null, null, null, null, null, null, null, false), new ProductMutationResult(ProductMutationStatus.Success), ProductMutationResultFailure(), QueryCache.Products);
        await AssertInvalidates(services, new DeleteProductCommand(1), ProductMutationStatus.Success, ProductMutationStatus.Conflict, QueryCache.Products);
        await AssertInvalidates(services, new CreateSupplierCommand("Supplier", null, null, null, null, null, null, null, null, null, null), new SupplierMutationResult(SupplierMutationStatus.Success), new SupplierMutationResult(SupplierMutationStatus.NotFound), QueryCache.Suppliers);
        await AssertInvalidates(services, new UpdateSupplierCommand(1, "Supplier", null, null, null, null, null, null, null, null, null, null), new SupplierMutationResult(SupplierMutationStatus.Success), new SupplierMutationResult(SupplierMutationStatus.NotFound), QueryCache.Suppliers);
        await AssertInvalidates(services, new DeleteSupplierCommand(1), SupplierMutationStatus.Success, SupplierMutationStatus.Conflict, QueryCache.Suppliers);
    }

    [Fact]
    public async Task GetEndpointsRetainSuccessNotFoundAndValidationContracts()
    {
        using var services = CreateServices();
        var mediator = services.GetRequiredService<IMediator>();
        Assert.Equal(200, ((IStatusCodeHttpResult)await GetAllCustomersEndpoint.Handle(mediator, TestToken)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await GetProductsEndpoint.Handle(mediator, TestToken)).StatusCode);
        Assert.Equal(200, ((IStatusCodeHttpResult)await GetSuppliersEndpoint.Handle(mediator, TestToken)).StatusCode);
        for (var i = 0; i < 2; i++)
        {
            var product = await GetProductByIdEndpoint.Handle(1, mediator, new GetProductByIdQueryValidator(), TestToken);
            Assert.Equal(200, ((IStatusCodeHttpResult)product).StatusCode);
            Assert.Equal(Product, ((IValueHttpResult)product).Value);
            var supplier = await GetSupplierByIdEndpoint.Handle(1, mediator, new GetSupplierByIdQueryValidator(), TestToken);
            Assert.Equal(Supplier, ((IValueHttpResult)supplier).Value);
            var customer = await GetCustomerByIdEndpoint.Handle("ALFKI", mediator, new GetCustomerByIdQueryValidator(), TestToken);
            Assert.Equal("ALFKI", Assert.IsType<GetCustomerByIdQueryResponse>(((IValueHttpResult)customer).Value).CustomerId);
        }
        Assert.Equal(404, ((IStatusCodeHttpResult)await GetProductByIdEndpoint.Handle(2, mediator, new GetProductByIdQueryValidator(), TestToken)).StatusCode);
        Assert.Equal(404, ((IStatusCodeHttpResult)await GetSupplierByIdEndpoint.Handle(2, mediator, new GetSupplierByIdQueryValidator(), TestToken)).StatusCode);
        Assert.Equal(404, ((IStatusCodeHttpResult)await GetCustomerByIdEndpoint.Handle("XXXXX", mediator, new GetCustomerByIdQueryValidator(), TestToken)).StatusCode);
        Assert.Equal(400, ((IStatusCodeHttpResult)await GetProductByIdEndpoint.Handle(0, mediator, new GetProductByIdQueryValidator(), TestToken)).StatusCode);
        Assert.Equal(400, ((IStatusCodeHttpResult)await GetSupplierByIdEndpoint.Handle(0, mediator, new GetSupplierByIdQueryValidator(), TestToken)).StatusCode);
        Assert.Equal(400, ((IStatusCodeHttpResult)await GetCustomerByIdEndpoint.Handle("bad", mediator, new GetCustomerByIdQueryValidator(), TestToken)).StatusCode);
    }

    private static ProductMutationResult ProductMutationResultFailure() => new(ProductMutationStatus.InvalidSupplier);

    private static async Task AssertInvalidates<TResponse>(
        ServiceProvider services, IInvalidatesCache<TResponse> command, TResponse success, TResponse failure, string tag)
    {
        var cache = services.GetRequiredService<HybridCache>();
        var queries = new (string Key, string Tag)[]
        {
            (new GetAllProductsQuery().CacheKey, QueryCache.Products),
            (new GetProductByIdQuery(1).CacheKey, QueryCache.Products),
            (new GetProductByIdQuery(2).CacheKey, QueryCache.Products),
            (new GetAllSuppliersQuery().CacheKey, QueryCache.Suppliers),
            (new GetSupplierByIdQuery(1).CacheKey, QueryCache.Suppliers),
            (new GetAllCustomersQuery().CacheKey, QueryCache.Customers),
        };
        foreach (var query in queries)
            await cache.SetAsync(query.Key, "old", tags: [query.Tag], cancellationToken: TestToken);
        var behavior = new CacheInvalidationBehavior<IInvalidatesCache<TResponse>, TResponse>(
            cache, NullLogger<CacheInvalidationBehavior<IInvalidatesCache<TResponse>, TResponse>>.Instance,
            services.GetRequiredService<CacheInvalidationState>());
        Assert.Equal(failure, await behavior.Handle(command, _ => Task.FromResult(failure), TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            command, _ => throw new InvalidOperationException("Write failed"), TestToken));
        foreach (var query in queries)
            Assert.Equal("old", await cache.GetOrCreateAsync(query.Key, _ => ValueTask.FromResult("new"), cancellationToken: TestToken));

        using var caller = new CancellationTokenSource();
        var calls = 0;
        Assert.Equal(success, await behavior.Handle(command, token =>
        {
            Assert.Equal(caller.Token, token);
            calls++;
            caller.Cancel();
            return Task.FromResult(success);
        }, caller.Token));
        Assert.Equal(1, calls);
        foreach (var query in queries)
            Assert.Equal(query.Tag == tag ? "new" : "old", await cache.GetOrCreateAsync(query.Key, _ => ValueTask.FromResult("new"), cancellationToken: TestToken));
    }

    private static async Task AssertCached<TResponse>(ServiceProvider services, ICacheable<TResponse> query, TResponse response)
    {
        var calls = 0;
        Task<TResponse> Handler(CancellationToken token)
        {
            calls++;
            return Task.FromResult(response);
        }
        Assert.Equal(response, await Execute(services, query, Handler));
        Assert.Equal(response, await Execute(services, query, Handler));
        Assert.Equal(1, calls);
    }

    private static async Task AssertNotCached<TResponse>(ServiceProvider services, ICacheable<TResponse> query, TResponse response)
    {
        var calls = 0;
        Task<TResponse> Handler(CancellationToken token)
        {
            calls++;
            return Task.FromResult(response);
        }
        Assert.Equal(response, await Execute(services, query, Handler));
        Assert.Equal(response, await Execute(services, query, Handler));
        Assert.Equal(2, calls);
    }

    private static Task<TResponse> Execute<TResponse>(ServiceProvider services, ICacheable<TResponse> query, RequestHandlerDelegate<TResponse> handler) =>
        Execute(services, query, handler, TestToken);

    private static async Task<TResponse> Execute<TResponse>(ServiceProvider services, ICacheable<TResponse> query, RequestHandlerDelegate<TResponse> handler, CancellationToken token)
    {
        services.GetRequiredService<TestHandlers>().Callbacks[query.GetType()] = handler;
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(query, token);
    }

    private static ServiceProvider CreateServices(CacheClock? clock = null, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<CacheInvalidationState>();
        services.AddScoped<CacheExecutionContext>();
        services.AddSingleton<TestHandlers>();
        services.AddScoped<ScopeLifetime>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HybridCache:DefaultEntryOptions:Expiration"] = "00:01:00",
            ["HybridCache:DefaultEntryOptions:LocalCacheExpiration"] = "00:01:00",
        }).Build();
        services.Configure<HybridCacheOptions>(configuration.GetSection("HybridCache"));
        if (clock is not null)
            services.Configure<MemoryCacheOptions>(options => options.Clock = clock);
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<GetAllProductsQuery>();
            configuration.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(CachingBehavior<,>));
        });
        AddHandler<GetAllProductsQuery, IReadOnlyList<ProductResponse>>(services, _ => [Product]);
        AddHandler<GetProductByIdQuery, ProductResponse?>(services, q => q.ProductId == 1 ? Product : null);
        AddHandler<GetAllSuppliersQuery, IReadOnlyList<SupplierResponse>>(services, _ => [Supplier]);
        AddHandler<GetSupplierByIdQuery, SupplierResponse?>(services, q => q.SupplierId == 1 ? Supplier : null);
        AddHandler<GetAllCustomersQuery, IEnumerable<GetAllCustomersQueryResponse>>(services, _ => [new() { CustomerId = "ALFKI" }]);
        AddHandler<GetCustomerByIdQuery, GetCustomerByIdQueryResponse?>(services, q => q.Id == "ALFKI" ? new() { CustomerId = "ALFKI" } : null);
        AddHandler<ProbeQuery, ProbeResponse>(services, _ => new(true));
        AddHandler<PingCommand, PingCommandResponse>(services, _ => new("hello"));
        AddHandler<DeleteProductCommand, ProductMutationStatus>(services, _ => ProductMutationStatus.Success);
        AddHandler<UpdateProductCommand, ProductMutationResult>(services, _ => new(ProductMutationStatus.Success));
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void AddHandler<TRequest, TResponse>(IServiceCollection services, Func<TRequest, TResponse> fallback)
        where TRequest : IRequest<TResponse> =>
        services.AddScoped<IRequestHandler<TRequest, TResponse>>(provider =>
            new Handler<TRequest, TResponse>(provider.GetRequiredService<TestHandlers>(), provider.GetRequiredService<ScopeLifetime>(), fallback));

    private sealed record ProbeResponse(bool Success);

    private sealed record ProbeQuery(TimeSpan? Expiration = null) : ICacheable<ProbeResponse>
    {
        public string CacheKey => QueryCache.Key<ProbeQuery>();
        public bool IsSuccessful(ProbeResponse response) => response.Success;
    }

    private sealed class CacheClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class TestHandlers
    {
        public ConcurrentDictionary<Type, Delegate> Callbacks { get; } = new();
        public Action<ScopeLifetime>? OnHandle { get; set; }
    }

    private sealed class ScopeLifetime : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentBag<Dictionary<string, object?>> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary());
    }

    private sealed class Handler<TRequest, TResponse>(TestHandlers handlers, ScopeLifetime lifetime, Func<TRequest, TResponse> fallback) : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
        {
            handlers.OnHandle?.Invoke(lifetime);
            var response = handlers.Callbacks.TryGetValue(typeof(TRequest), out var callback)
                ? await ((RequestHandlerDelegate<TResponse>)callback)(cancellationToken)
                : fallback(request);
            Assert.False(lifetime.IsDisposed);
            return response;
        }
    }
}
