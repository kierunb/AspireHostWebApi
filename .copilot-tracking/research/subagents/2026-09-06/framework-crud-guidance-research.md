<!-- markdownlint-disable-file -->

# Framework CRUD Guidance for Suppliers

## Research scope

Research date: 2026-09-06.

Topics:

* ASP.NET Core response semantics for create, get, update, and delete endpoints
* EF Core asynchronous querying and `ExecuteDeleteAsync` compared with tracked deletion under referential constraints
* AutoMapper `ProjectTo` query projection behavior
* FluentValidation asynchronous validation considerations
* MediatR request and handler conventions
* Reprise endpoint route and parameter conventions, where discoverable
* Alternative route and contract shapes for Suppliers CRUD in this .NET 10 repository

## Repository baseline

The application targets `net10.0`. Relevant direct package references are
AutoMapper 16.2.0, FluentValidation 12.1.1, MediatR 14.2.0, Reprise 3.7.0, and
`Aspire.Microsoft.EntityFrameworkCore.SqlServer` 13.5.3. The resolved EF Core
version is 10.0.11, as recorded in
`WebApiMediatorCQRS/obj/project.assets.json`.

The existing Products CRUD is the nearest implementation precedent:

* Reprise endpoint classes use `[Endpoint]` and HTTP method attributes such as
  `[Get("/products/{id:int}")]`; `Program.cs` calls `builder.ConfigureServices()`
  and `app.MapEndpoints()`.
* Create returns `201 Created` with `/products/{id}` as `Location` and the
  response DTO as the body.
* Get-by-id returns `200 OK` or `404 Not Found`.
* Full update uses `PUT`, returns the updated DTO with `200 OK`, and returns
  `404 Not Found` for an absent entity.
* Delete returns `204 No Content`, `404 Not Found`, or a `409 Conflict` problem
  response when order details reference the product.
* Endpoint methods manually call FluentValidation `ValidateAsync` because the
  registered MediatR `ValidationBehavior<,>` is disabled.
* Read handlers use `AsNoTracking`, deterministic ordering for collections,
  `ProjectTo`, and EF Core asynchronous terminal operators with cancellation.
* Update and delete handlers load tracked entities with `FindAsync`; delete
  checks known dependents before `Remove`/`SaveChangesAsync` and also catches
  `DbUpdateException` for a race or unanticipated constraint.

The generated Northwind model has an optional `Products.SupplierId` foreign key
and no configured cascade delete on `Products.Supplier`. `Suppliers.Products`
is the corresponding collection. The database model therefore does not express
ownership of Products by Supplier, and Supplier deletion needs an explicit
contract for existing Products rather than an assumed cascade.

## Findings

### ASP.NET Core response semantics

Reprise maps endpoint methods into ASP.NET Core Minimal API route handlers, so
ASP.NET Core result behavior and HTTP semantics remain the controlling rules.
Typed results improve OpenAPI response metadata and make the declared outcome
set visible in the endpoint signature, but `IResult` remains appropriate when a
branch uses an untyped helper such as `Results.ValidationProblem`.

For a server-assigned Supplier ID, `POST /suppliers` is the natural collection
operation. RFC 9110 recommends `201 Created` with a `Location` identifying the
primary resource. ASP.NET Core's `TypedResults.Created` supports that shape and
can also return the created DTO. Returning the DTO avoids an immediate GET and
matches the Product endpoint.

`GET /suppliers/{id:int}` should return `200 OK` with the representation or
`404 Not Found`. For `PUT`, RFC 9110 defines the request body as the desired
replacement state and permits `200 OK` with content or `204 No Content` when an
existing resource is modified. Returning `200` with the updated DTO is the more
consistent local choice. `PATCH` is the correct method only if the project
introduces an explicit partial-update document and its associated media type,
validation, and merge semantics.

A completed synchronous delete with no response body should return `204 No
Content`. A missing Supplier can return `404`, as Product deletion does. The
idempotence of DELETE concerns repeated server effects, not identical response
codes, so a later `404` does not violate HTTP idempotence. Returning `204` for an
already absent Supplier is also defensible when non-disclosure or retry
simplicity outweighs the local convention. A Supplier that still has Products
fits `409 Conflict`: the current resource state prevents the operation, and the
response should identify the dependency so the client can resolve it.

Both `400 Bad Request` and `422 Unprocessable Content` can represent invalid
input. RFC 9110 gives `422` a precise semantic-content meaning, while this
application and Reprise already expose validation failures as `400` problem
details. Retaining `400` with `ValidationProblemDetails` preserves one error
contract unless the API adopts `422` consistently across all resources.

### EF Core querying and deletion

EF Core's asynchronous terminal operators should be used for database I/O:
`ToListAsync`, `SingleOrDefaultAsync`, `AnyAsync`, `SaveChangesAsync`, and
`ExecuteDeleteAsync`. LINQ operators such as `Where`, `OrderBy`, and `ProjectTo`
only build the expression and do not need asynchronous variants. Pass the
request cancellation token to terminal operations. EF Core does not support
multiple parallel operations on one `DbContext`, so dependent checks and writes
must be awaited sequentially unless separate contexts are deliberately used.

Read-only Supplier queries should use `AsNoTracking`, stable ordering for the
collection, projection to response DTOs, and an asynchronous terminal operator.
Projection is preferable to materializing generated entity graphs because the
response requires only selected columns.

Tracked deletion is the strongest fit for this repository:

* Load the Supplier with `FindAsync` to distinguish `404` from success.
* Query `Products.AnyAsync(product => product.SupplierId == id)` to produce the
  deliberate `409` contract without loading the collection.
* Call `Remove` and `SaveChangesAsync` only after the dependency check.
* Catch the relevant `DbUpdateException` around the save as protection against
  a Product inserted between the check and delete or another database
  constraint not represented by the pre-check.

`ExecuteDeleteAsync` is a viable alternative when one SQL statement and no
entity materialization are more valuable. It executes immediately, bypasses
the change tracker and `SaveChanges`, and returns the affected row count. A
predicate by ID can therefore map zero affected rows to `404`. It does not,
however, perform the Product conflict pre-check, coordinate with tracked state,
or provide automatic concurrency control. SQL Server will still enforce the
foreign key, so the endpoint must translate the expected database exception to
`409`. Mixing tracked mutations and `ExecuteDeleteAsync` on the same entities
can leave the tracker stale.

The model has a nullable `Products.SupplierId` and no explicit `OnDelete`
configuration for that relationship. EF Core convention uses client-side null
propagation for an optional relationship when dependent Products are tracked,
but the database foreign key has neither cascade delete nor `ON DELETE SET
NULL`. The current delete flow does not load all dependents, so nullable does
not make database deletion automatically null their foreign keys. The explicit
repository policy should therefore be "reject while Products reference the
Supplier." Automatically nulling all Products would be a different business
operation and should be documented and performed transactionally. Cascade
deletion would imply Supplier ownership of Products and conflicts with the
Northwind model.

### AutoMapper projection

AutoMapper's queryable extension emits a LINQ `Select` so EF Core can translate
the projection and retrieve only the columns required by the DTO. It avoids
materializing the full Supplier entity and does not require `Include` merely to
map navigations that are part of a provider-translatable projection.

`ProjectTo` must be the last query-shaping operation. Filter, sort, and page the
entity query first, then project, then invoke `ToListAsync` or
`SingleOrDefaultAsync`. This matters because the provider understands the entity
model, while later composition over DTO members may not translate as intended.

Projection mappings must be expression based and translatable by EF Core.
Explicit conversions may be required. Runtime-only features such as Func-based
`MapFrom`, custom resolvers, custom type converters, `BeforeMap`, and `AfterMap`
are not supported in query projection. For a flat Supplier DTO with matching
properties, the current profile and `ProjectTo` pattern is appropriate. Map a
tracked entity in memory for create and update responses, as the Product flow
does, rather than applying `ProjectTo` to a single already-loaded object.

### FluentValidation asynchronous validation

FluentValidation requires `ValidateAsync` whenever a validator contains
`MustAsync`, `CustomAsync`, or `WhenAsync`; calling `Validate` throws for such a
validator. `ValidateAsync` also runs synchronous rules, so endpoints can use it
uniformly and pass the request cancellation token.

Manual validation is the clearest Minimal API integration and is supported by
FluentValidation's current guidance. The ASP.NET MVC validation pipeline is
synchronous, MVC-only, and no longer recommended for new projects; it cannot
run asynchronous validation rules. Reprise offers its own validation filter,
but its 3.7.0 documentation labels filter support as .NET 7 only and predates
this .NET 10 application. The existing explicit validation in Product endpoints
is therefore the lower-risk precedent.

Supplier commands should have validators for route IDs and writable fields.
Route ID and body ID should not be two independently writable sources. Prefer a
request DTO without `SupplierId`, constructing the command from the route ID and
body. If the body retains an ID, reject a mismatch explicitly. Database-backed
uniqueness or existence rules can be asynchronous, but mutation handlers still
need to enforce database constraints because validation and persistence are not
atomic.

### MediatR requests and handlers

MediatR uses `IRequest<TResponse>` with one
`IRequestHandler<TRequest, TResponse>` for request/response commands and queries;
the handler receives a `CancellationToken`. `ISender.Send(request,
cancellationToken)` is the narrower endpoint dependency, although preserving
the repository's existing `IMediator` usage is more consistent and has no
material effect on the Supplier slice.

Keep HTTP concepts in the endpoint and persistence outcomes in the handler.
The Product implementation's mutation result containing a status plus an
optional DTO is a useful local convention: handlers can report success,
not-found, or conflict without returning `IResult`, while endpoints map those
outcomes to HTTP. Query handlers should return DTOs or null, not EF entities.

The repository registers handlers by assembly scan and explicitly adds only
`LoggingBehavior<,>`. MediatR behaviors are not discovered merely because their
classes exist. Validation and caching remain disabled, so Supplier endpoints
must not assume either cross-cutting behavior runs. MediatR 14 also has current
license-key configuration and warning behavior; this is an operational caveat,
not a reason to alter the local command/query design.

### Reprise routes and parameters

Reprise 3.7.0 discovers a public class marked `[Endpoint]` with a public static
`Handle` method marked by `[Get]`, `[Post]`, `[Put]`, `[Patch]`, or `[Delete]`.
Handlers may be asynchronous and use Minimal API binding: route parameters bind
by matching name, a complex request DTO binds from JSON, and registered services
bind from dependency injection. The current application activates discovery
through `builder.ConfigureServices()` and `app.MapEndpoints()`.

Use explicit route constraints such as `{id:int}` to match Product routes and
prevent an unparseable ID from entering the handler. Keep route parameter names
aligned with `Handle` parameter names. A cancellation token is supplied by
ASP.NET Core and should flow through validation, MediatR, and EF Core.

Reprise can attach OpenAPI metadata through its own `ProducesAttribute` and
`NameAttribute`, but typed results already contribute response metadata in
modern ASP.NET Core. Do not mix Reprise's `ProducesAttribute` with the MVC type
without verifying generated OpenAPI. Reprise's self-checks detect duplicate
method/route combinations and malformed endpoint classes at startup.

## Contract alternatives for Suppliers CRUD

| Concern | Recommended consistency shape | Defensible alternative | Decision effect |
|---|---|---|---|
| Collection route | `/suppliers` | `/api/suppliers` or versioned `/api/v1/suppliers` | Use the existing unprefixed Product convention unless API-wide versioning is introduced |
| Item route | `/suppliers/{id:int}` | Natural-key route such as `/suppliers/{companyName}` | Numeric key matches the database identity and avoids mutable, encoded names |
| Create | `POST /suppliers`, `201` + `Location` + DTO | Client-selected ID via `PUT /suppliers/{id}` | Northwind uses a database-generated integer, so POST is the honest contract |
| Read collection | `GET /suppliers`, `200` array | Envelope with paging metadata | An array matches current simple reads; add an envelope when paging/filtering becomes a requirement |
| Update | `PUT /suppliers/{id:int}`, `200` DTO | `204`, upsert via PUT, or `PATCH` | `200` mirrors Products; avoid upsert and PATCH unless their contracts are intentionally added |
| Delete missing item | `404` | Idempotent-looking `204` | Both preserve DELETE idempotence; `404` matches the Product endpoint |
| Delete with Products | `409` problem details | Null product links, cascade Products, or forbid DELETE entirely | `409` preserves data and lets the client resolve dependencies explicitly |
| Validation failure | `400` validation problem | `422` validation problem | `400` matches Reprise and existing endpoints; move to `422` only API-wide |
| Delete implementation | Tracked entity + dependency pre-check | `ExecuteDeleteAsync` + row count + FK exception translation | Tracked flow expresses `404` and `409` directly and matches Products |
| Concurrency | Current last-write-wins behavior | Rowversion/ETag with `If-Match`, returning `412` on stale writes | Add conditional requests only with a real concurrency token and API-wide contract |

## Recommended approach for this repository

Mirror the Products vertical slice with Supplier-specific request and response
types, mappings, validators, MediatR handlers, and five Reprise endpoints:

* `GET /suppliers` returns a deterministically ordered, no-tracking projected
  list.
* `GET /suppliers/{id:int}` returns a projected DTO or `404`.
* `POST /suppliers` validates a body without an ID and returns `201`,
  `/suppliers/{newId}`, and the created DTO.
* `PUT /suppliers/{id:int}` performs full replacement of writable fields and
  returns `200` with the updated DTO or `404`.
* `DELETE /suppliers/{id:int}` returns `204`, `404`, or `409` when Products
  reference the Supplier.

Use `ProjectTo` only on EF `IQueryable` reads and keep it last before the async
terminal operation. Use tracked entities for create, update, and delete. Validate
explicitly with `ValidateAsync`, forward cancellation tokens, and keep HTTP result
mapping in endpoints. Preserve the generated database files; Supplier CRUD does
not require changing entity classes or `NorthwindContext`.

This is a consistency recommendation, not a claim that every existing Product
choice is universally optimal. A future API-wide design could add paging
envelopes, `422`, PATCH documents, ETags, or versioned route groups, but adding
any one of those only for Suppliers would create a less predictable API.

## Sources and version caveats

Sources were accessed on 2026-09-06 unless a publication date is stated.

* [ASP.NET Core Minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0)
  documents `IResult`, `Results`, and `TypedResults` for ASP.NET Core 10.
* [ASP.NET Core Minimal API parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0)
  documents route, body, service, and cancellation-token binding for ASP.NET
  Core 10.
* [EF Core asynchronous programming](https://learn.microsoft.com/en-us/ef/core/miscellaneous/async)
  distinguishes expression-building LINQ operators from async terminal I/O and
  warns against parallel operations on one context.
* [EF Core efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
  recommends projecting only required properties and using no-tracking queries
  for read-only work.
* [EF Core execute update and delete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
  documents immediate execution, affected-row counts, tracker bypass, and the
  lack of automatic concurrency control for `ExecuteDeleteAsync`.
* [EF Core cascade delete](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete)
  explains database versus client cascade/null behavior and FK failures.
* [AutoMapper queryable extensions source](https://raw.githubusercontent.com/LuckyPennySoftware/AutoMapper/master/docs/source/Queryable-Extensions.md)
  documents SQL projection, provider limitations, final-operation placement,
  and unsupported runtime mapping options. The repository uses AutoMapper
  16.2.0; the cited source is the current main-branch documentation, so validate
  complex expressions against the installed version and EF provider.
* [FluentValidation asynchronous validation](https://docs.fluentvalidation.net/en/latest/async.html)
  requires `ValidateAsync` for asynchronous rules.
* [FluentValidation ASP.NET Core integration](https://docs.fluentvalidation.net/en/latest/aspnet.html)
  recommends manual Minimal API validation and describes the MVC pipeline's
  asynchronous limitation. The repository uses 12.1.1, which targets .NET 8 or
  later and is compatible with .NET 10.
* [MediatR repository documentation](https://github.com/LuckyPennySoftware/MediatR)
  and [MediatR request/response wiki](https://github.com/LuckyPennySoftware/MediatR/wiki)
  document assembly registration, request and handler interfaces, Send, and
  cancellation tokens. The repository uses 14.2.0 and should account for that
  version's license configuration.
* [Reprise 3.7.0 package](https://www.nuget.org/packages/Reprise/3.7.0)
  documents endpoint discovery, routes, Minimal API binding, validation, and
  OpenAPI conventions. Version 3.7.0 was last updated on 2023-07-16, targets
  .NET 6, describes ASP.NET Core 6/7, and has no newer package release visible.
  It is compatible at the target-framework level with .NET 10, but its filters,
  metadata integration, and startup behavior require regression coverage
  because the documentation predates ASP.NET Core 8 through 10.
* [RFC 9110, HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html),
  published June 2022, defines POST, PUT, DELETE, idempotence, Location, and the
  relevant status codes.
* Local evidence: `WebApiMediatorCQRS/WebApiMediatorCQRS.csproj`,
  `WebApiMediatorCQRS/obj/project.assets.json`,
  `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs`,
  `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs`,
  `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs`,
  `WebApiMediatorCQRS/Commands/ProductCommands.cs`,
  `WebApiMediatorCQRS/Queries/ProductQueries.cs`,
  `WebApiMediatorCQRS/Database/Suppliers.cs`, and
  `WebApiMediatorCQRS/Database/NorthwindContext.cs`.

## Remaining gaps and clarifying questions

The framework evidence answers the requested approach choices. The following
product decisions cannot be inferred from the repository:

* Whether deleting a Supplier with Products must always be rejected, or whether
  the business wants a separate reassignment or unlink workflow
* Whether Supplier collection volume requires pagination, filtering, and a
  response envelope now
* Whether clients require partial updates or optimistic concurrency; neither is
  present in the Product contract

Before implementation, verify Reprise 3.7.0 startup discovery and generated
OpenAPI on .NET 10, then exercise all response branches against a reachable
Northwind database. The highest-value automated coverage would target route
binding, validation problem details, `201 Location`, missing items, and the
Supplier-with-Products delete conflict, including the race fallback through
`DbUpdateException`.
