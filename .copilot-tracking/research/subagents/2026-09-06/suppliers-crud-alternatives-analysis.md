<!-- markdownlint-disable-file -->
# Suppliers CRUD Technical Scenario Analysis

## Research Topics

* Compare a Product-style vertical slice, an MVC controller, and a broader pipeline refactor
* Compare tracked delete with a Products pre-check and `409`, `ExecuteDelete` with exception translation, nulling `Product.SupplierId`, and cascade delete
* Compare PUT replacement, PATCH, and upsert semantics
* Compare explicit endpoint validation and enabling MediatR `ValidationBehavior`
* Compare integration-test scopes under Aspire and external SQL Server LocalDB constraints
* Select one cohesive implementation approach and define its impact and focused validation matrix

## Status

Complete. The analysis evaluates 16 candidate choices across five decision areas. It selects one cohesive approach and rejects 11 alternatives or variants. No application code was modified.

## Executive Decision

Implement Suppliers as a Product-style Reprise and MediatR vertical slice. Use flat Supplier request and response records, projected no-tracking reads, tracked writes, replacement-style `PUT`, explicit endpoint validation, and a guarded tracked delete that returns `409 Conflict` while Products reference the Supplier. Add fast validator and handler tests that do not require Aspire where practical, plus a focused Aspire HTTP suite that runs only against a reachable, isolated Northwind-compatible SQL Server database.

This choice minimizes architectural change while making the Supplier-specific dependency policy explicit. It also preserves the existing public conventions: `201` plus `Location` on create, `200` or `404` on reads and replacement, `204` on delete, `400` validation problems, and `409` for a relational conflict.

## Repository Evidence

### Existing vertical-slice boundary

* Product uses separate response, create, and replacement request records without a body ID at `WebApiMediatorCQRS/ApiModels/ProductModels.cs:3-41`.
* Product commands colocate mutation statuses, validators, tracked handlers, referential checks, and cancellable EF operations at `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248`.
* Product reads use `AsNoTracking`, deterministic ID ordering, `ProjectTo`, and cancellable terminal operations at `WebApiMediatorCQRS/Queries/ProductQueries.cs:11-53`.
* Product Reprise routes and HTTP translation are implemented at `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:8-45`, `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-76`, `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-56`, and `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-37`.
* AutoMapper needs only the entity-to-response convention map at `WebApiMediatorCQRS/Profiles/ProductProfile.cs:7-13`.
* Assembly scanning registers MediatR handlers, validators, AutoMapper profiles, and Reprise endpoints at `WebApiMediatorCQRS/Program.cs:9-9,37-55`; routes are mapped at `WebApiMediatorCQRS/Program.cs:77-83`.

### Supplier schema and dependency

* `Suppliers` is generated code and exposes scalar fields plus the Products navigation at `WebApiMediatorCQRS/Database/Suppliers.cs:1-35`.
* `CompanyName` is required and limited to 40 characters. Other mapped lengths are Address 60, City 15, ContactName 30, ContactTitle 30, Country 15, Fax 24, Phone 24, PostalCode 10, and Region 15; HomePage is `ntext` at `WebApiMediatorCQRS/Database/NorthwindContext.cs:317-342`.
* `Products.SupplierId` is nullable at `WebApiMediatorCQRS/Database/Products.cs:8-34`, but the relationship has no explicit cascade or set-null delete behavior at `WebApiMediatorCQRS/Database/NorthwindContext.cs:256-291`.
* Generated nullable annotations are disabled, so API nullability must follow EF configuration rather than the plain `string` declarations at `WebApiMediatorCQRS/Database/Suppliers.cs:1-32`.

### Validation and exception behavior

* Only `LoggingBehavior<,>` is enabled. `ValidationBehavior<,>` and `CachingBehavior<,>` are commented out at `WebApiMediatorCQRS/Program.cs:37-44`; validators are still assembly-registered at `WebApiMediatorCQRS/Program.cs:46-47`.
* Product endpoints call `ValidateAsync` before MediatR dispatch and pass the cancellation token at `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:14-36`, `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:15-39`, and `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:15-29`.
* The dormant behavior runs all validators and throws `FluentValidation.ValidationException` at `WebApiMediatorCQRS/Behaviors/ValidationBehavior.cs:6-36`.
* The global handler gives thrown validation failures a different payload shape and otherwise exposes the exception message as the title at `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:6-40`. It has no specific `DbUpdateException` translation.

### MVC and test baselines

* MVC is registered and mapped at `WebApiMediatorCQRS/Program.cs:16-18,81-82`, but its only controller is a Ping example under `/mvc` with three POST demonstrations at `WebApiMediatorCQRS/Controllers/PingController.cs:9-43`. It is not the repository's CRUD precedent.
* The existing test starts the AppHost, waits for `webapimediatorcqrs`, and asserts only `/swagger` at `WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-39`.
* The test project references AppHost rather than the API and includes Aspire Hosting Testing at `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:1-31`.
* AppHost provisions only the API resource, not SQL Server or Northwind, at `AspireAppHost/AspireAppHost.AppHost/AppHost.cs:1-5`.
* The application database is external LocalDB `(localdb)\sql2025` with integrated security at `WebApiMediatorCQRS/appsettings.json:8-10`.

## Technical Scenario Analysis

### Scenario 1: Supplier feature architecture

#### Requirements

* Preserve the repository's ASP.NET Core, Reprise, MediatR, AutoMapper, FluentValidation, and EF Core boundaries.
* Avoid direct edits to EF Core Power Tools output.
* Keep Supplier HTTP semantics predictable beside Product CRUD.

#### Preferred approach

Mirror the Product vertical slice with `SupplierModels`, `SupplierCommands`, `SupplierQueries`, `SupplierProfile`, and five discoverable Reprise endpoint classes. Keep HTTP result construction in endpoints and database outcomes in handlers.

#### Implementation details

Assembly scanning means no production registration change is expected when new types stay in the API assembly. Supplier DTOs should expose the 12 scalar columns, omit `Products`, and keep `SupplierId` route-controlled and response-only. Reads should filter or order entities before `ProjectTo`. Writes should map explicitly into tracked entities and save with the request cancellation token.

#### Considered alternatives

1. Product-style vertical slice, selected. It is the nearest complete CRUD implementation, demonstrated by `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248` and `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:8-45`.
2. MVC controller, rejected. MVC is technically available, but the only controller is a Ping demonstration at `WebApiMediatorCQRS/Controllers/PingController.cs:9-43`; choosing MVC for Supplier would split otherwise parallel Product and Supplier CRUD conventions.
3. Broader pipeline or architecture refactor, rejected. Existing scans and endpoint mapping already support the feature at `WebApiMediatorCQRS/Program.cs:37-55,77-83`. Refactoring behaviors, result types, route groups, or all Product code expands blast radius without being required for Supplier CRUD.

### Scenario 2: Delete policy and persistence strategy

#### Requirements

* Distinguish missing Supplier from a Supplier blocked by existing Products.
* Do not delete Products as a side effect.
* Handle the race between dependency inspection and deletion.
* Preserve a stable HTTP contract despite database constraint enforcement.

#### Preferred approach

Load the Supplier with cancellable `FindAsync`, query `Products.AnyAsync(product => product.SupplierId == id)`, return a conflict status when referenced, then call `Remove` and `SaveChangesAsync`. Catch the expected referential `DbUpdateException` around the save as a race fallback and translate it to `409` in the endpoint.

The pre-check gives a deliberate domain result without loading the navigation. The exception fallback covers a Product inserted after the check. The endpoint should return `204`, `404`, or Supplier-specific `409 ProblemDetails`, mirroring Product deletion at `WebApiMediatorCQRS/Commands/ProductCommands.cs:204-248` and `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-37`.

#### Implementation details

The handler should avoid catching every database failure as a conflict if provider-specific inspection can reliably identify a foreign-key violation. If the first implementation follows Product exactly and catches broad `DbUpdateException`, tests and logging must acknowledge that connectivity or unrelated write failures could be mislabeled. No direct generated model or migration change is part of this approach.

#### Considered alternatives

1. Tracked delete with Products pre-check and `409`, selected. It matches the existing relational conflict flow and makes the business policy explicit.
2. `ExecuteDeleteAsync` with exception translation, rejected for this feature. It can use affected rows for `404` and save one entity read, but bypasses tracking and relies on an exception for the normal dependency branch. The current global handler cannot translate `DbUpdateException` at `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:29-40`, so local translation would still be required and would diverge from Product.
3. Null all `Product.SupplierId` values before deletion, rejected. Nullability at `WebApiMediatorCQRS/Database/Products.cs:12-12` makes this technically possible, but no repository evidence says deletion means unlinking every Product. It is a different multi-row business operation and would require an explicit transaction, audit expectations, and dedicated client contract.
4. Cascade delete Products, rejected. The relationship at `WebApiMediatorCQRS/Database/NorthwindContext.cs:287-291` does not configure cascade, and Products have independent CRUD and OrderDetails dependencies at `WebApiMediatorCQRS/Commands/ProductCommands.cs:204-248`. Supplier does not own Product lifecycle.

### Scenario 3: Update contract

#### Requirements

* Keep one authoritative Supplier identity source.
* Define omitted and null optional-field behavior.
* Preserve predictable idempotent update semantics.

#### Preferred approach

Use `PUT /suppliers/{id:int}` as full replacement of every writable scalar. The body omits `SupplierId`; the route ID controls lookup. Return `200` with the updated response, `404` when absent, and `400` for invalid input. Supplying null for an optional field clears it.

This directly matches the Product request declaration at `WebApiMediatorCQRS/ApiModels/ProductModels.cs:29-41`, full scalar assignment at `WebApiMediatorCQRS/Commands/ProductCommands.cs:185-200`, and HTTP result mapping at `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-56`.

#### Implementation details

Document replacement semantics in API XML documentation and the Supplier HTTP examples. Because omitted JSON properties bind to defaults, clients must send the desired complete representation. There is no concurrency token in `WebApiMediatorCQRS/Database/Suppliers.cs:8-35`, so updates remain last-write-wins unless a separate concurrency feature is designed later.

#### Considered alternatives

1. Replacement-style PUT, selected. It preserves Product symmetry and has unambiguous null-clearing behavior.
2. PATCH, rejected. No patch document type, JSON Patch package, merge-patch media type, field-presence tracking, or PATCH precedent exists. Adding PATCH only for Supplier would require new binding, validation, OpenAPI, and null-versus-omitted semantics.
3. PUT upsert, rejected. Supplier IDs appear store-generated by EF convention at `WebApiMediatorCQRS/Database/NorthwindContext.cs:317-325`; create already belongs at the collection POST route. Upsert would let a missing route ID change PUT from replacement to creation and needs client-selected-key semantics not supported by repository evidence.

### Scenario 4: Validation placement

#### Requirements

* Prevent invalid Supplier input from reaching EF Core.
* Keep error payloads consistent with Product endpoints.
* Avoid validating the same command twice.

#### Preferred approach

Inject `IValidator<TCommand>` or `IValidator<TQuery>` into each input-bearing Reprise endpoint and call `ValidateAsync` with the request cancellation token before `mediator.Send`. Return `Results.ValidationProblem(validationResult.ToDictionary())` on failure.

Validators should enforce positive route IDs, nonempty `CompanyName`, and the exact schema-derived maximum lengths. `HomePage` should remain nullable without an invented maximum or URL rule until a product requirement exists.

#### Implementation details

Create and update validators may share rule construction only if it remains clearer than duplication; no cross-cutting behavior change is needed. List retrieval has no input validator. Handler-level database constraints remain authoritative for races even when endpoint validation succeeds.

#### Considered alternatives

1. Explicit endpoint validation, selected. It matches Product at `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:14-36` and produces the established validation-problem dictionary.
2. Enable MediatR `ValidationBehavior<,>`, rejected for Supplier scope. Registration is disabled at `WebApiMediatorCQRS/Program.cs:37-44`. Enabling it affects all requests, duplicates current endpoint validation unless Product is refactored, and routes failures through the different global payload at `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:13-28`.

### Scenario 5: Verification under Aspire and external LocalDB

#### Requirements

* Keep deterministic checks available when LocalDB is absent.
* Verify actual HTTP binding and status semantics through Reprise.
* Protect existing Northwind data from destructive test collisions.
* Exercise the Supplier-to-Product conflict on the real SQL Server provider.

#### Preferred approach

Use a layered suite:

1. Add fast tests for Supplier validators and pure HTTP outcome helpers. Add focused handler tests only with a relational provider that preserves required SQL behavior; do not claim EF InMemory proves FK or SQL Server semantics.
2. Extend Aspire integration coverage with a Supplier CRUD test collection that uses an explicitly configured disposable or isolated Northwind-compatible database. Serialize database-mutating tests, create uniquely named rows, capture generated IDs, and clean up in reverse dependency order.
3. Keep the existing `/swagger` startup smoke test separate. Gate database-backed tests with an explicit environment/configuration precondition and report them as skipped when no test database is available, rather than silently targeting the developer's default catalog.

#### Implementation details

The HTTP matrix should use `app.CreateHttpClient("webapimediatorcqrs")` after health readiness, following `WebApiMediatorCQRS.Tests/IntegrationTests.cs:22-35`. The AppHost does not provision SQL at `AspireAppHost/AspireAppHost.AppHost/AppHost.cs:1-5`, so test isolation needs either an AppHost database resource added as separate infrastructure work or an externally supplied test connection. The checked-in LocalDB connection at `WebApiMediatorCQRS/appsettings.json:8-10` must not be treated as disposable.

#### Considered alternatives

1. Layered fast checks plus gated Aspire and SQL Server integration, selected. It separates deterministic contract checks from provider-dependent facts.
2. Aspire startup and Swagger smoke only, rejected as insufficient. The current test at `WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-39` proves neither CRUD nor database connectivity.
3. Full CRUD tests against the default developer LocalDB, rejected as unsafe and environment-dependent. AppHost supplies no isolated database, and the default connection uses a persistent Northwind catalog.
4. Handler-only tests using EF Core InMemory, rejected as the sole strategy. They would not verify Reprise routing, Problem Details, SQL Server lengths, generated identity, foreign-key enforcement, or delete-race translation.

## Selected Approach

The cohesive choice is:

* Product-style Reprise and MediatR vertical slice
* Flat Supplier contracts with route-controlled, response-only `SupplierId`
* No-tracking, ordered, projected reads
* Tracked create, replacement-style tracked PUT, and tracked guarded delete
* Delete policy of `409 Conflict` while any Product references the Supplier
* Explicit endpoint `ValidateAsync` calls with cancellation propagation
* Layered tests, with provider-independent checks always available and SQL Server CRUD tests gated on an isolated database

The approach intentionally leaves broader concerns unchanged: no pipeline-wide validation migration, no generated EF edits, no cascade rule, no implicit unlinking, no PATCH, no upsert, no pagination, and no concurrency-token design.

## Implementation Impact

### Production additions

* `WebApiMediatorCQRS/ApiModels/SupplierModels.cs`: response, create request, and replacement request records
* `WebApiMediatorCQRS/Commands/SupplierCommands.cs`: mutation statuses and results, create/update/delete commands, validators, and handlers
* `WebApiMediatorCQRS/Queries/SupplierQueries.cs`: list and item queries, positive-ID validator, and projected handlers
* `WebApiMediatorCQRS/Profiles/SupplierProfile.cs`: `Suppliers` to `SupplierResponse` map
* `WebApiMediatorCQRS/Endpoints/CreateSupplierEndpoint.cs`: POST and `201 Location`
* `WebApiMediatorCQRS/Endpoints/GetSuppliersEndpoint.cs`: collection and item GET
* `WebApiMediatorCQRS/Endpoints/UpdateSupplierEndpoint.cs`: replacement PUT
* `WebApiMediatorCQRS/Endpoints/DeleteSupplierEndpoint.cs`: `204`, `404`, and Supplier-specific `409`
* `WebApiMediatorCQRS/Suppliers.http`: successful flow plus validation, missing, null-clearing, and conflict examples

### Test additions or configuration

* Add an API project reference to the test project for direct validator and handler access if fast tests are implemented there
* Add Supplier-focused test files and a serialized database test fixture
* Add explicit test-database configuration or Aspire-provisioned SQL infrastructure before enabling destructive CRUD tests in automation
* Consider aligning `Aspire.Hosting.Testing` 13.4.6 at `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:12-18` with AppHost 13.5.3 as separate dependency maintenance, not a Supplier feature prerequisite

### Existing files expected to remain unchanged

* `WebApiMediatorCQRS/Database/Suppliers.cs`
* `WebApiMediatorCQRS/Database/Products.cs`
* `WebApiMediatorCQRS/Database/NorthwindContext.cs`
* `WebApiMediatorCQRS/Program.cs`, unless the team separately chooses pipeline-wide validation or test connection plumbing

## Focused Validation Matrix

| Layer | Case | Expected result | Database required |
|---|---|---|---|
| Build | Build the full solution | All projects compile; endpoint discovery types and mappings resolve | No |
| Validator | Nonpositive item ID | Invalid result keyed to Supplier ID | No |
| Validator | Null, empty, or whitespace CompanyName | Invalid result | No |
| Validator | CompanyName and optional strings at exact limits | Valid result | No |
| Validator | Each bounded field one character over its limit | Invalid result keyed to that field | No |
| Mapping | Supplier entity to response | All scalar fields map; Products is absent | No |
| HTTP | `GET /suppliers` | `200`, array ordered by Supplier ID | Yes |
| HTTP | `GET /suppliers/{id}` existing and missing | `200` with DTO; `404` | Yes |
| HTTP | GET with ID zero | `400` validation problem | No row access expected |
| HTTP | `POST /suppliers` valid | `201`, DTO, matching `/suppliers/{id}` Location | Yes |
| HTTP | POST invalid lengths or CompanyName | `400`; no row created | Yes for side-effect assertion |
| HTTP | `PUT /suppliers/{id}` valid replacement | `200`; every writable scalar replaced | Yes |
| HTTP | PUT clears optional values with nulls | `200`; subsequent GET returns nulls | Yes |
| HTTP | PUT missing Supplier | `404`; no row created | Yes |
| HTTP | `DELETE /suppliers/{id}` unreferenced | `204`; subsequent GET is `404` | Yes |
| HTTP | DELETE missing Supplier | `404` | Yes |
| HTTP | DELETE Supplier referenced by Product | `409` Problem Details; Supplier and Product remain | Yes |
| Persistence | Product inserted between pre-check and save | FK exception translated to `409`; no Supplier deletion | Yes, controlled concurrency |
| Contract | Swagger/OpenAPI document | All five routes and declared statuses are present | Running AppHost |
| Safety | Cancellation during validation or EF operation | Cancellation propagates; no replacement token is used | Depends on case |

The minimum implementation gate is full solution build, fast validator checks, successful CRUD over an isolated SQL Server database, one invalid payload, one missing item, null-clearing PUT, and referenced-Supplier delete conflict. The race test is valuable but may require a controllable handler-level interception or transaction fixture.

## Unresolved Runtime-Only Facts

* Whether `dbo.Suppliers.SupplierID` is actually `IDENTITY`; EF convention suggests generated integer keys, but only connected database metadata can prove it
* The deployed `FK_Products_Suppliers` delete action; the generated model contains no explicit cascade or set-null action, but SQL catalog metadata is authoritative
* Availability of `(localdb)\sql2025` and the Northwind catalog on each developer or CI machine
* Reprise 3.7.0 discovery, route-binding failure behavior, and generated OpenAPI details on .NET 10
* Exact SQL Server exception number and constraint metadata available through EF Core 10 for narrowly identifying the expected Supplier foreign-key conflict
* Runtime validation payload casing and shape across Reprise model binding, explicit validation, and exception handling
* Whether the external test database can be reset safely and whether concurrent test runs receive isolated catalogs
* Business policy for HomePage URL/length validation, whitespace normalization, duplicate CompanyName values, and optimistic concurrency; the schema and Product precedent do not decide these

## References

### Existing research

* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md:1-214`
* `.copilot-tracking/research/subagents/2026-09-06/product-crud-pattern-research.md:1-293`
* `.copilot-tracking/research/subagents/2026-09-06/supplier-schema-registration-tests-research.md:1-181`
* `.copilot-tracking/research/subagents/2026-09-06/framework-crud-guidance-research.md:1-300`

### Primary repository evidence

* `WebApiMediatorCQRS/ApiModels/ProductModels.cs:3-41`
* `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248`
* `WebApiMediatorCQRS/Queries/ProductQueries.cs:11-53`
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-76`
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:8-45`
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-56`
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-37`
* `WebApiMediatorCQRS/Profiles/ProductProfile.cs:7-13`
* `WebApiMediatorCQRS/Database/Suppliers.cs:1-35`
* `WebApiMediatorCQRS/Database/Products.cs:1-35`
* `WebApiMediatorCQRS/Database/NorthwindContext.cs:256-291,317-342`
* `WebApiMediatorCQRS/Behaviors/ValidationBehavior.cs:6-36`
* `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:6-40`
* `WebApiMediatorCQRS/Controllers/PingController.cs:9-43`
* `WebApiMediatorCQRS/Program.cs:9-83`
* `AspireAppHost/AspireAppHost.AppHost/AppHost.cs:1-5`
* `WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-39`
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:1-31`
* `WebApiMediatorCQRS/appsettings.json:8-10`

## Recommended Next Research Not Completed

* Query connected SQL Server catalog views for Supplier identity and FK delete metadata
* Start the AppHost and inspect runtime Reprise route discovery and OpenAPI output
* Establish an isolated SQL Server test catalog and verify reset and parallel-run behavior
* Prototype narrow SQL Server foreign-key exception classification before replacing the Product-style broad catch

## Clarifying Questions

* Must Supplier deletion always preserve Product links by returning `409`, or does the product owner require an explicit unlink or reassignment workflow?
* Should HomePage, phone, postal code, and CompanyName receive policies beyond schema-derived nullability and lengths?
* Is a disposable SQL Server or LocalDB database available for CI, or must AppHost provision one before CRUD integration tests are enabled?