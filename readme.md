# WebApi with .NET Aspire

This sample demostrates how to build simple REST Api with ASP.NET Core .NET Aspire using patterns and libraries like:
- CQRS with MediatR
- REPR Pattern with Minimal API and Reprise
- Automatic mapping with AutoMapper
- Observability with OpenTelemetry
- Data validation with FluentValidation
- Data access with Entity Framework Core
- Global Exception Handling
- GET query caching with .NET HybridCache

## Query caching

The customer, product, and supplier list/detail GET queries use the MediatR
`CachingBehavior` and an in-process `HybridCache`. HTTP output caching is not
used. Status codes and response models are unchanged.

`HybridCache:DefaultEntryOptions:Expiration` and
`HybridCache:DefaultEntryOptions:LocalCacheExpiration` in
`WebApiMediatorCQRS/appsettings.json` default to five minutes. Override them with
environment-specific settings or environment variables such as
`HybridCache__DefaultEntryOptions__LocalCacheExpiration=00:01:00`.
Other `HybridCacheOptions` can also be configured in this section.

Read-only requests opt in with `ICacheable<TResponse>`: use
`QueryCache.Key<TQuery>(parameters)` with every parameter affecting the result,
optional resource `CacheTags`, and an optional absolute `Expiration` override.
A null expiration uses the configured defaults; HybridCache does not use sliding
expiration. Null results are never cached. Queries returning a result envelope
must override `IsSuccessful` to reject unsuccessful results. Exceptions (including
validation failures) are not stored, and invalid detail-query parameters bypass
caching. HTTP validation continues to run before MediatR.

HybridCache coalesces concurrent misses. Shared work runs in its own DI scope
and forwards the combined cancellation token through MediatR to the handler.
The scoped `CacheExecutionContext` prevents recursive caching during this inner
dispatch. Keep validation/authorization behaviors outside the caching behavior
if those are added to the pipeline.

Successful product and supplier commands implement `IInvalidatesCache<TResponse>`
and invalidate their resource tag, including all list and detail entries.
Invalidation advances a process-local resource generation so an older in-flight
read cannot populate the keys used by later requests. Post-commit invalidation
finishes even if the caller disconnects. Failed commands do not invalidate.
Customer mutations are not currently exposed; any future customer command must
invalidate `QueryCache.Customers`.

Debug cache logs include the operation and query type, never keys, IDs, or
response contents. Successful invalidations are logged at Information level.
This setup is single-process: external database changes remain visible only
after expiration, and multiple API instances do not share invalidation. A
distributed provider and cross-instance invalidation require separate design.