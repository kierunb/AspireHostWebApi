<!-- markdownlint-disable-file -->

# Product CRUD Pattern Research for Suppliers

## Research Scope

* Analyze the existing Product CRUD vertical slice as the nearest implementation model for Suppliers CRUD.
* Record exact workspace-relative paths and 1-based line ranges.
* Capture contracts, routes, status codes, validation, EF Core access, AutoMapper, cancellation, and inconsistencies.
* Compare the Product patterns with the generated `Suppliers` entity.

## Findings

### Executive conclusion

The Product slice is the nearest complete CRUD implementation model for Suppliers. It
uses three API records, MediatR commands and queries, colocated FluentValidation
validators, Reprise endpoint classes, one convention-based AutoMapper profile, and
direct EF Core access through `NorthwindContext`. Supplier CRUD can preserve this
shape, but its delete dependency is `Products.SupplierId`, and its request nullability
must follow EF configuration rather than the nullable-oblivious generated entity.

The Product slice does not rely on the MediatR validation pipeline. Every endpoint
that has a validator resolves it from dependency injection and calls `ValidateAsync`
before `mediator.Send`. `GetAllProductsQuery` is the only operation with no validator.

### Request and response contracts

Source: `WebApiMediatorCQRS/ApiModels/ProductModels.cs:3-41`.

| Contract | Fields | Purpose |
|----------|--------|---------|
| `ProductResponse` | `ProductId: int`, `ProductName: string`, `SupplierId: int?`, `CategoryId: int?`, `QuantityPerUnit: string?`, `UnitPrice: decimal?`, `UnitsInStock: short?`, `UnitsOnOrder: short?`, `ReorderLevel: short?`, `Discontinued: bool` | Single-item and list output |
| `CreateProductRequest` | All response fields except `ProductId` | Create body |
| `UpdateProductRequest` | Same fields as create, also excluding `ProductId` | Full replacement body; identity comes only from the route |

The create and update records deliberately duplicate their shapes. The API project has
nullable reference types enabled at
`WebApiMediatorCQRS/WebApiMediatorCQRS.csproj:3-7`, so `ProductName` is statically
non-null while `QuantityPerUnit` is explicitly nullable. ASP.NET Core's default JSON
naming presents these positional record properties as camel case, as demonstrated by
`WebApiMediatorCQRS/Products.http:11-22` and
`WebApiMediatorCQRS/Products.http:31-42`.

No navigation properties are exposed. `Category` and `Supplier` from the generated
Product entity and its `OrderDetails` collection remain persistence-only concerns.

### Commands and mutation results

Source: `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248`.

`ProductMutationStatus` at lines 10-17 is a shared closed status vocabulary:
`Success`, `NotFound`, `InvalidSupplier`, `InvalidCategory`, and `Conflict`.
`ProductMutationResult` at lines 19-22 combines a status with an optional
`ProductResponse`; delete returns only `ProductMutationStatus`.

Create behavior at lines 24-119:

* `CreateProductCommand` repeats the create request shape at lines 24-34.
* Its validator at lines 36-49 requires a nonempty `ProductName` of at most 40
	characters; optional supplier and category IDs must be greater than zero; optional
	quantity text is limited to 20 characters; and optional price, stock, order, and
	reorder values must be nonnegative. `Discontinued` has no rule.
* The handler checks supplier existence first and category existence second with two
	possible `AnyAsync` calls at lines 92-117. It short-circuits on the first invalid
	reference.
* It constructs `Products` manually at lines 70-81, calls `Products.Add` at line 83,
	and calls `SaveChangesAsync(cancellationToken)` at line 84.
* After persistence assigns the key, it convention-maps the tracked entity to
	`ProductResponse` at lines 86-89.

Update behavior at lines 121-202:

* `UpdateProductCommand` adds route-derived `ProductId` to the replacement shape at
	lines 121-132.
* Its validator at lines 134-148 adds `ProductId > 0` and otherwise duplicates create
	validation exactly.
* The handler uses primary-key `FindAsync([request.ProductId], cancellationToken)` at
	lines 160-163. Tracking is intentional because it mutates the returned entity.
* A missing entity returns `NotFound` before supplier and category checks. Existing
	optional references are checked separately with `AnyAsync` at lines 167-183.
* Every mutable scalar is assigned directly at lines 185-193, including assigning
	nulls. The endpoint is therefore replacement-style `PUT`, not a patch.
* One `SaveChangesAsync(cancellationToken)` persists the tracked changes at line 195,
	then AutoMapper creates the response at lines 197-200.

Delete behavior at lines 204-248:

* `DeleteProductCommand` carries only `ProductId`; its validator requires a positive
	ID at lines 206-212.
* The handler locates the tracked product with `FindAsync` at lines 222-225 and returns
	`NotFound` when absent.
* It explicitly checks `OrderDetails.AnyAsync` at lines 229-232 and returns `Conflict`
	if the product is referenced.
* It removes the tracked entity at line 236. `SaveChangesAsync` is wrapped in a broad
	`DbUpdateException` catch at lines 238-246 to translate a race or another database
	update failure into `Conflict`.

### Queries and EF Core read patterns

Source: `WebApiMediatorCQRS/Queries/ProductQueries.cs:11-53`.

The list query at lines 11-27 returns
`IReadOnlyList<ProductResponse>`. Its handler composes one server-side query:

1. `Products.AsNoTracking()` avoids change tracking.
2. `OrderBy(product => product.ProductId)` makes list order deterministic.
3. `ProjectTo<ProductResponse>(mapper.ConfigurationProvider)` projects only the DTO
	 fields in SQL.
4. `ToListAsync(cancellationToken)` executes asynchronously.

The by-ID query at lines 29-53 returns nullable `ProductResponse`. Its validator at
lines 31-37 requires `ProductId > 0`. The handler uses `AsNoTracking`, filters by ID,
projects before materialization, and calls `SingleOrDefaultAsync(cancellationToken)`.
It does not load a Product entity or navigation properties.

The unused `request` parameter in the list handler is required by the MediatR handler
signature. There is no pagination, filtering, total count, or result envelope.

### AutoMapper use

`WebApiMediatorCQRS/Profiles/ProductProfile.cs:7-13` defines only
`CreateMap<Products, ProductResponse>()`. Matching property names and compatible types
make configuration unnecessary. The same map serves two distinct paths:

* `ProjectTo<ProductResponse>` for SQL projection in queries
* `mapper.Map<ProductResponse>(product)` after create and update saves

There are no request-to-command, request-to-entity, reverse, or navigation maps.
Supplier DTO scalar names should therefore exactly match the generated scalar property
names if the same minimal profile is used.

AutoMapper discovers the profile through assembly scanning at
`WebApiMediatorCQRS/Program.cs:49-52`.

### Routes, endpoint classes, and status codes

All Product routes are registered through Reprise assembly discovery. Reprise service
configuration is at `WebApiMediatorCQRS/Program.cs:54-55`; endpoint mapping is at
`WebApiMediatorCQRS/Program.cs:80-80`.

| Endpoint class | Method and template | Success | Validation and domain outcomes |
|----------------|---------------------|---------|--------------------------------|
| `GetProductsEndpoint` | `GET /products` | `200 OK`, body is a JSON array of `ProductResponse` | No Product-specific error outcome |
| `GetProductByIdEndpoint` | `GET /products/{id:int}` | `200 OK`, body is `ProductResponse` | `400` for integer ID less than or equal to zero; `404` when no row exists |
| `CreateProductEndpoint` | `POST /products` | `201 Created`, body is `ProductResponse`, `Location` is `/products/{newId}` | `400` for command validation, missing supplier, or missing category |
| `UpdateProductEndpoint` | `PUT /products/{id:int}` | `200 OK`, body is `ProductResponse` | `400` for command validation, missing supplier, or missing category; `404` when product is absent |
| `DeleteProductEndpoint` | `DELETE /products/{id:int}` | `204 No Content` | `400` for nonpositive integer ID; `404` when absent; `409 Conflict` when referenced by order details or a caught update exception |

Endpoint evidence:

* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:8-21` implements list.
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:23-45` implements by-ID.
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-55` implements create.
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-56` implements update.
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-37` implements delete.
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:57-76` holds shared invalid
	reference and conflict response helpers used across three endpoint files.

There are no explicit endpoint route names such as `WithName("GetProductById")` or a
named-route attribute. The only names are endpoint class names and literal route
templates. The `{id:int}` route constraint means a noninteger path segment does not
reach FluentValidation; it normally fails route matching rather than producing the
endpoint's explicit `400` validation response.

The `[Produces]` attributes describe status codes but do not specify response body
types. Every handler returns the broad `IResult` rather than a typed result union, so
the method signature also does not expose the response alternatives to OpenAPI.

### Validation and problem response behavior

Validators are registered by assembly scan at
`WebApiMediatorCQRS/Program.cs:46-47`. MediatR and logging behavior are registered at
`WebApiMediatorCQRS/Program.cs:37-43`, but `ValidationBehavior<,>` is commented out at
line 42. Consequently:

* Create, get-by-ID, update, and delete inject `IValidator<T>` and call
	`ValidateAsync(..., cancellationToken)` before dispatch.
* List dispatches immediately because `GetAllProductsQuery` has no input.
* Validation failures use `Results.ValidationProblem(validationResult.ToDictionary())`
	and return HTTP 400 without invoking handlers.
* Missing supplier or category references are handler outcomes translated by
	`ProductEndpointResults.InvalidReference` into HTTP 400 validation problems. The
	dictionary key is produced by `nameof(request.SupplierId)` or
	`nameof(request.CategoryId)`, so it is PascalCase even though JSON body properties
	are camelCase.
* Delete conflicts use RFC-style `ProblemDetails` with status 409, title
	`The product cannot be deleted.`, and a detail naming the product ID and order-detail
	dependency at `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:67-75`.

The dormant pipeline behavior at
`WebApiMediatorCQRS/Behaviors/ValidationBehavior.cs:6-36` would run all validators in
parallel and throw `FluentValidation.ValidationException`. The global handler at
`WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:6-40` translates that exception
to a differently shaped 400 response. Enabling the behavior without removing manual
endpoint validation would duplicate validation and change response formatting on any
path that bypasses the endpoint check.

### Cancellation flow

Each endpoint accepts the request-abort `CancellationToken` and passes the same token
to manual validation and `mediator.Send`. Each handler passes its MediatR token to all
EF Core terminal operations:

* `AnyAsync` reference and dependency checks
* `FindAsync`
* `ToListAsync`
* `SingleOrDefaultAsync`
* `SaveChangesAsync`

No operation creates a replacement token, suppresses cancellation, or uses
`CancellationToken.None`. AutoMapper's in-memory `Map` calls do not need a token, and
`ProjectTo` remains deferred until the cancellable EF terminal call.

### HTTP call sites and test coverage

`WebApiMediatorCQRS/Products.http:1-45` is the only Product-specific caller found. It
uses `http://localhost:5039`, creates one product with null foreign keys, captures the
created ID through VS Code REST Client response syntax, gets it, fully replaces it,
and deletes it. The sequence covers only successful outcomes and contains no response
assertions.

No Product route appears in source tests. The only integration test,
`WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-38`, starts the Aspire application and
asserts only that `GET /swagger` returns 200. Generated `bin` and `obj` matches were
excluded from this conclusion.

### Generated Suppliers entity and EF configuration

The generated entity is at `WebApiMediatorCQRS/Database/Suppliers.cs:8-35`:

| Property | CLR shape in generated file | EF constraint |
|----------|-----------------------------|---------------|
| `SupplierId` | `int` | Primary key, column `SupplierID`; integer key generation follows EF convention |
| `CompanyName` | `string` | Required, maximum 40 |
| `ContactName` | `string` | Optional in EF, maximum 30 |
| `ContactTitle` | `string` | Optional in EF, maximum 30 |
| `Address` | `string` | Optional in EF, maximum 60 |
| `City` | `string` | Optional in EF, maximum 15 |
| `Region` | `string` | Optional in EF, maximum 15 |
| `PostalCode` | `string` | Optional in EF, maximum 10 |
| `Country` | `string` | Optional in EF, maximum 15 |
| `Phone` | `string` | Optional in EF, maximum 24 |
| `Fax` | `string` | Optional in EF, maximum 24 |
| `HomePage` | `string` | Optional in EF, SQL `ntext`, no configured maximum |
| `Products` | `ICollection<Products>` | Inverse navigation, not an API scalar |

EF evidence is at `WebApiMediatorCQRS/Database/NorthwindContext.cs:317-338`. The
`Suppliers` DbSet is declared at line 32. `CompanyName` and `PostalCode` have indexes
at lines 321-323, but neither index is configured as unique.

The generated file starts with `#nullable disable` at
`WebApiMediatorCQRS/Database/Suppliers.cs:1-2`. Its plain `string` declarations do not
mean all database columns are required. A Supplier API contract in this nullable-enabled
project should use `string CompanyName` and nullable `string?` for all other scalar
text fields. It should omit `Products` from request and response DTOs unless the API
explicitly chooses an expanded representation.

### Supplier adaptation of the Product pattern

The closest Supplier vertical slice would use:

* `SupplierResponse`, `CreateSupplierRequest`, and `UpdateSupplierRequest` records with
	the 12 scalar columns, excluding `SupplierId` from request records.
* `SupplierMutationStatus` with at least `Success`, `NotFound`, and `Conflict`, plus a
	nullable response in `SupplierMutationResult` if the Product result pattern is kept.
	Supplier create and update have no analogous incoming foreign keys, so
	`InvalidSupplier` and `InvalidCategory` should not be copied.
* Create and update validators with `CompanyName.NotEmpty().MaximumLength(40)` and the
	exact optional maximum lengths from EF configuration. `HomePage` has no database
	maximum to mirror.
* Read handlers using `Suppliers.AsNoTracking()`, ID ordering, `ProjectTo`, and
	cancellable async materialization.
* Create using a manually initialized `Suppliers` entity; update using cancellable
	`FindAsync`, direct assignments, and one cancellable save.
* Delete using a tracked `FindAsync`, then
	`Products.AnyAsync(product => product.SupplierId == request.SupplierId,
	cancellationToken)` before removal. The Product-to-Supplier FK is configured at
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:288-291` and the inverse navigation
	is at `WebApiMediatorCQRS/Database/Suppliers.cs:34-34`.
* A `DbUpdateException` catch around Supplier delete save as a race-condition fallback,
	with Supplier-specific 409 title and detail stating that products reference the
	supplier.
* Reprise routes `GET /suppliers`, `GET /suppliers/{id:int}`,
	`POST /suppliers`, `PUT /suppliers/{id:int}`, and
	`DELETE /suppliers/{id:int}` if route symmetry is the goal.

No direct edits should be made to `Suppliers.cs` or `NorthwindContext.cs`; both are
generated by EF Core Power Tools and can be overwritten by regeneration.

### Pitfalls and inconsistencies not to copy blindly

* Manual validation is an architectural requirement under current registration, not
	optional endpoint ceremony. Omitting it lets invalid commands reach EF because the
	MediatR validation behavior is disabled.
* Create and update validators duplicate most rules. This is simple and local but can
	drift when schema constraints change.
* Referential `AnyAsync` checks and `SaveChangesAsync` are separate round trips with no
	transaction spanning them. A referenced row can disappear between check and save.
	Product create and update do not catch the resulting `DbUpdateException`.
* Delete's broad `catch (DbUpdateException)` reports every update failure as a
	dependency conflict, potentially hiding connectivity or unrelated database errors.
* The pre-delete dependency check and exception fallback are intentionally redundant:
	the first gives a clear domain outcome, while the second handles races. Supplier
	deletion needs both if it mirrors current behavior.
* `ProductEndpointResults` lives in the create endpoint file but is used by update and
	delete. A direct Supplier copy would create the same cross-file ownership surprise.
* Validation-problem keys for invalid references use CLR PascalCase names, while JSON
	request properties are camelCase.
* Product list reads are unbounded. Copying the query is suitable for the small
	Northwind sample, not automatically for a large Supplier table.
* No uniqueness rule exists for Product name or Supplier company name. The database
	indexes are not unique, so duplicate company names are valid under current schema.
* The request records are positional and nonnullable `CompanyName` would still be
	vulnerable to runtime null input. FluentValidation `NotEmpty` is required to enforce
	the contract at runtime.
* `PUT` replaces nullable columns with null when null is supplied. It should not be
	presented as partial update behavior.
* OpenAPI metadata lists status codes but does not strongly declare response schemas or
	explicit operation names.
* The `.http` flow and automated tests leave invalid IDs, null and oversized strings,
	not-found behavior, FK validation, delete conflicts, response bodies, and
	cancellation unverified.

## References

* `WebApiMediatorCQRS/ApiModels/ProductModels.cs:3-41`: Product API records
* `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248`: mutation status, commands,
	validators, handlers, EF writes, and delete conflict handling
* `WebApiMediatorCQRS/Queries/ProductQueries.cs:11-53`: list and by-ID query patterns
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-76`: create route and shared
	Product endpoint result helpers
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:8-45`: list and by-ID routes
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-56`: replacement route
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-37`: delete route
* `WebApiMediatorCQRS/Profiles/ProductProfile.cs:7-13`: convention map
* `WebApiMediatorCQRS/Products.http:1-45`: successful manual CRUD flow
* `WebApiMediatorCQRS/Program.cs:37-55`: MediatR, validator, AutoMapper, and Reprise
	registration
* `WebApiMediatorCQRS/Program.cs:80-80`: Reprise endpoint mapping
* `WebApiMediatorCQRS/Behaviors/ValidationBehavior.cs:6-36`: available but disabled
	pipeline validation implementation
* `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:6-40`: exception-to-problem
	response behavior
* `WebApiMediatorCQRS/Database/Products.cs:8-35`: generated Product entity shape
* `WebApiMediatorCQRS/Database/Suppliers.cs:8-35`: generated Supplier entity shape
* `WebApiMediatorCQRS/Database/NorthwindContext.cs:256-293`: Product columns and
	category/supplier relationships
* `WebApiMediatorCQRS/Database/NorthwindContext.cs:317-338`: Supplier key, indexes,
	required field, lengths, and SQL type
* `WebApiMediatorCQRS/WebApiMediatorCQRS.csproj:3-16`: target framework, nullable mode,
	and relevant package versions
* `WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-38`: current integration coverage

## Remaining Gaps

* No live database or endpoint execution was performed because the request was
	research-only. Runtime response payload casing and Reprise-generated OpenAPI operation
	IDs were inferred from project conventions and source registration.
* The database's deployed FK delete action was not inspected directly. The EF model
	defines the optional Product-to-Supplier relationship without an explicit
	`OnDelete`; Supplier delete should therefore rely on an explicit dependency check and
	database-exception fallback rather than assume cascade behavior.
* No Supplier API requirements specify whether `HomePage` should receive an
	application-level maximum despite SQL `ntext`, or whether phone, postal code, and
	country fields need format validation beyond database lengths.

## Recommended Next Research

* Inspect the generated OpenAPI document at runtime to confirm Reprise operation IDs,
	response schemas, and model-binding errors before treating Product metadata as an
	exact public contract.
* Verify the actual SQL Server `FK_Products_Suppliers` delete action against the target
	Northwind database.
* Confirm desired Supplier-specific validation policy for phone, postal code, URL, and
	whitespace normalization.
* Define focused integration cases for all five Supplier routes, especially delete
	conflict, nonpositive ID, missing row, maximum lengths, and nullable field clearing.

## Clarifying Questions

* Should Supplier `HomePage` remain unrestricted to mirror the database, or receive an
	API-level length and URL policy?
* Should Supplier company names remain nonunique as the current Northwind schema allows,
	or should the API impose uniqueness beyond the Product pattern?
