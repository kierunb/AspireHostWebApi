using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace WebApiMediatorCQRS.Behaviors;

public interface ICacheable<TResponse> : IRequest<TResponse>
{
    string CacheKey { get; }
    TimeSpan? Expiration => null;
    IEnumerable<string> CacheTags => [];
    bool BypassCache => false;
    bool IsSuccessful(TResponse response) => response is not null;
}

public static class QueryCache
{
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Suppliers = "suppliers";

    public static string Key<TQuery>(params object?[] parameters) =>
        $"{typeof(TQuery).FullName}:v1:{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parameters)))}";
}

public sealed class CacheExecutionContext
{
    public bool IsExecuting { get; set; }
}

public sealed class CacheInvalidationState
{
    private readonly ConcurrentDictionary<string, long> generations = new(StringComparer.Ordinal);

    public string GetKey(string key, IEnumerable<string> tags) =>
        QueryCache.Key<CacheInvalidationState>(
            key,
            tags.Order(StringComparer.Ordinal)
                .Select(tag => new { Tag = tag, Generation = generations.GetValueOrDefault(tag) })
                .ToArray()
        );

    public void Invalidate(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
            generations.AddOrUpdate(tag, 1, static (_, generation) => generation + 1);
    }
}

public sealed class CachingBehavior<TRequest, TResponse>(
    ILogger<CachingBehavior<TRequest, TResponse>> logger,
    HybridCache cache,
    IServiceScopeFactory scopeFactory,
    CacheExecutionContext executionContext,
    CacheInvalidationState invalidationState
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (executionContext.IsExecuting || request is not ICacheable<TResponse> cacheable || cacheable.BypassCache)
            return await next(cancellationToken);

        var tags = cacheable.CacheTags.ToArray();
        // A pre-mutation factory may finish later, but cannot populate the current generation.
        var key = invalidationState.GetKey(cacheable.CacheKey, tags);
        var options = cacheable.Expiration is { } expiration
            ? new HybridCacheEntryOptions
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration,
            }
            : null;

        logger.LogDebug("Cache lookup for {QueryType}", typeof(TRequest).Name);
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    logger.LogDebug("Cache miss for {QueryType}", typeof(TRequest).Name);
                    // Shared work must outlive any individual caller's request scope.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<CacheExecutionContext>().IsExecuting = true;
                    var response = await scope.ServiceProvider.GetRequiredService<ISender>().Send(cacheable, token);
                    if (!cacheable.IsSuccessful(response))
                    {
                        // A failed factory is not stored, including for other stampede waiters.
                        throw new UnsuccessfulResponseException(response);
                    }

                    logger.LogDebug("Caching successful result for {QueryType}", typeof(TRequest).Name);
                    return response;
                },
                options,
                tags,
                cancellationToken
            );
        }
        catch (UnsuccessfulResponseException exception)
        {
            logger.LogDebug("Not caching unsuccessful result for {QueryType}", typeof(TRequest).Name);
            return exception.Response;
        }
    }

    private sealed class UnsuccessfulResponseException(TResponse response) : Exception
    {
        public TResponse Response { get; } = response;
    }
}
