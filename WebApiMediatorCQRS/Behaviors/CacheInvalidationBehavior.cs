using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace WebApiMediatorCQRS.Behaviors;

public interface IInvalidatesCache<TResponse> : IRequest<TResponse>
{
    IEnumerable<string> CacheTags { get; }
    bool IsSuccessful(TResponse response);
}

public sealed class CacheInvalidationBehavior<TRequest, TResponse>(
    HybridCache cache,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger,
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
        var response = await next(cancellationToken);
        if (request is IInvalidatesCache<TResponse> invalidating && invalidating.IsSuccessful(response))
        {
            var tags = invalidating.CacheTags.ToArray();
            invalidationState.Invalidate(tags);
            // The write has committed; a disconnected caller must not leave stale entries behind.
            await cache.RemoveByTagAsync(tags, CancellationToken.None);
            logger.LogInformation("Invalidated cache after {CommandType}", typeof(TRequest).Name);
        }

        return response;
    }
}
