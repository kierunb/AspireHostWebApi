<!-- markdownlint-disable-file -->
# Suppliers CRUD Schema, Registration, and Tests Research

## Research Scope

* Program registration and middleware order
* Supplier and product model configuration, including foreign-key delete behavior
* Global exception handling and HTTP error translation
* Project targets and package versions
* Endpoint discovery
* Existing integration tests and test-project capabilities
* Solution membership and build or run constraints
* Concrete create, update, and delete risks for Suppliers CRUD

## Findings

### Registration and middleware

* `Program.cs` derives the domain assembly from `Program`, adds Aspire service defaults,
	registers `GlobalExceptionHandler` and Problem Details, MVC controllers, API explorer,
	Swagger, output caching, `NorthwindContext`, MediatR, FluentValidation, AutoMapper, and
	Reprise. Evidence: `WebApiMediatorCQRS/Program.cs:9-55`.
* The EF registration resolves connection name `NorthwindDB` and sets an EF command timeout
	of 15 seconds. The connection string also contains `Command Timeout=30`, so the effective
	timeout should be verified at runtime rather than inferred from the connection string.
	Evidence: `WebApiMediatorCQRS/Program.cs:31-35` and
	`WebApiMediatorCQRS/appsettings.json:8-11`.
* Only `LoggingBehavior<,>` is active. `ValidationBehavior<,>` and `CachingBehavior<,>` are
	commented out even though validators are discovered and registered. Supplier endpoints
	must therefore validate explicitly, as the product mutation endpoints do, unless the
	pipeline policy is changed for the whole application. Evidence:
	`WebApiMediatorCQRS/Program.cs:37-47`,
	`WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-36`,
	`WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-39`, and
	`WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-29`.
* `UseExceptionHandler` is installed before Swagger, HTTPS redirection, output cache,
	authorization, and endpoint execution. Reprise endpoints and MVC controllers are both
	mapped. `MapDefaultEndpoints` registers route endpoints before the middleware calls, but
	route mapping itself does not execute requests. Evidence: `WebApiMediatorCQRS/Program.cs:57-83`.
* Aspire service defaults add service discovery, standard HTTP resilience, OpenTelemetry,
	and a self liveness check. `/health` and `/alive` are mapped only in Development.
	Evidence: `AspireAppHost/AspireAppHost.ServiceDefaults/Extensions.cs:18-34` and
	`AspireAppHost/AspireAppHost.ServiceDefaults/Extensions.cs:100-126`.

### Endpoint discovery

* Reprise is registered with `builder.ConfigureServices()` and activated with
	`app.MapEndpoints()`. Existing product endpoint classes carry `[Endpoint]` and their
	handler methods carry `[Post]`, `[Put]`, or `[Delete]`. Evidence:
	`WebApiMediatorCQRS/Program.cs:54-55`, `WebApiMediatorCQRS/Program.cs:77-83`,
	`WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:9-13`,
	`WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:9-13`, and
	`WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:8-12`.
* No Supplier endpoint, request model, command, query, or profile exists. The only Supplier
	references outside `Database/` are product DTO and product command fields or FK checks.
	A Suppliers CRUD implementation must add discoverable endpoint classes and all feature
	types; naming a handler or adding an HTTP attribute alone is insufficient under the
	established Reprise pattern.

### Supplier and product schema

* `NorthwindContext.cs`, `Suppliers.cs`, and `Products.cs` carry EF Core Power Tools
	auto-generated headers and disable nullable analysis. The entity and context classes are
	partial, and `NorthwindContext` exposes `OnModelCreatingPartial` as the supported model
	extension seam. Direct edits are regeneration-prone. Evidence:
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:1-9`,
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:356-363`,
	`WebApiMediatorCQRS/Database/Suppliers.cs:1-10`, and
	`WebApiMediatorCQRS/Database/Products.cs:1-10`.
* EF Core Power Tools targets `Database`, includes `[dbo].[Suppliers]`, uses Fluent API,
	retains navigations, and sets `UseNullableReferences=false`. Evidence:
	`WebApiMediatorCQRS/efpt.config.json:2-9`,
	`WebApiMediatorCQRS/efpt.config.json:59-64`, and
	`WebApiMediatorCQRS/efpt.config.json:78-88`.
* `SupplierId` is the integer primary key. It is not marked `ValueGeneratedNever`, unlike
	the explicitly non-generated Region key. Under EF conventions an integer primary key is
	generated on add, but the repository does not prove that the deployed Northwind column
	is `IDENTITY`; database metadata remains the authority. Evidence:
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:317-325` and
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:295-302`.
* `CompanyName` is the only required Supplier string and has maximum length 40. The other
	mapped fields are optional at the database model level despite appearing as non-nullable
	CLR `string` properties because nullable references are disabled. Limits are Address 60,
	City 15, ContactName 30, ContactTitle 30, Country 15, Fax 24, Phone 24, PostalCode 10,
	Region 15; HomePage is `ntext`. Evidence:
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:325-342` and
	`WebApiMediatorCQRS/Database/Suppliers.cs:10-34`.
* `Products.SupplierId` is nullable. The Supplier relationship has no explicit `OnDelete`,
	while other generated relationships explicitly use `ClientSetNull` where required.
	There is no configured cascade delete for `FK_Products_Suppliers`. Evidence:
	`WebApiMediatorCQRS/Database/Products.cs:8-34` and
	`WebApiMediatorCQRS/Database/NorthwindContext.cs:256-291`.

### Concrete mutation risks

#### Create

* A create contract should omit `SupplierId` or prevent it from being mapped into a new
	entity. A nonzero supplied value could conflict with store-generated key semantics.
* `CompanyName` requires explicit non-empty and 40-character validation. Every other
	string should be nullable in API, command, response, and mapping types, with the mapped
	maximum lengths enforced before SQL Server rejects the write.
* Key generation must be confirmed against the actual Northwind schema. The Fluent model
	relies on convention rather than an explicit identity annotation.
* Without explicit validation, SQL truncation, nullability, or identity failures become
	`DbUpdateException` and fall through to a generic server error.

#### Update

* The route identifier should control lookup; accepting or mapping a body identifier risks
	changing key state or updating the wrong entity. Missing rows need an explicit 404 result.
* Optional columns need patch-versus-replace semantics. A full replacement command can
	erase existing optional values when omitted JSON properties bind as `null`.
* There is no concurrency token in the generated Supplier model, so concurrent updates use
	last-write-wins unless a separate policy is added. Evidence:
	`WebApiMediatorCQRS/Database/Suppliers.cs:8-35`.
* Product update handlers pre-check related IDs and return explicit statuses, demonstrating
	that database errors are not expected to define the HTTP contract. Evidence:
	`WebApiMediatorCQRS/Commands/ProductCommands.cs:151-204` and
	`WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:40-57`.

#### Delete

* A Supplier can be referenced by Products. Because the FK is nullable and no cascade is
	configured, deletion semantics are ambiguous: tracked dependents may be set to null by
	EF relationship fixup, while untracked rows can cause SQL Server referential-integrity
	failure. Lazy-loading proxies are not configured, so the navigation is not automatically
	loaded merely because it is virtual.
* The API must choose a policy: return 409 while products reference the Supplier, explicitly
	null all `Products.SupplierId` values before deletion, or alter the database/model delete
	behavior. Silent product deletion is not supported by the current model.
* A pre-check alone has a race with a product insert. The product delete implementation
	combines an existence pre-check with a `DbUpdateException` catch and maps conflict to
	HTTP 409; Supplier deletion needs equivalent race-safe translation. Evidence:
	`WebApiMediatorCQRS/Commands/ProductCommands.cs:206-244` and
	`WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:30-39`.

### Exception and HTTP translation

* `GlobalExceptionHandler` translates only thrown FluentValidation
	`ValidationException` to 400 and writes a flat string list under the `errors` extension.
	Evidence: `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:5-28`.
* Every other exception uses `exception.Message` as the public Problem Details title,
	inherits the current response status (normally 500 in the exception middleware), and has
	no type-specific mapping for `DbUpdateException`, not-found, or FK conflict. This can
	expose implementation messages and cannot provide a stable 404/409 CRUD contract.
	Evidence: `WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:29-40`.
* Logging records only the title and does not pass the exception object to `LogError`, so
	stack and inner-exception details may be absent from this handler's log entry. Evidence:
	`WebApiMediatorCQRS/Handlers/GlobalExceptionHandler.cs:30-37`.
* Existing product endpoints return validation problems, 404s, and 409s explicitly. Supplier
	endpoints should follow this behavior unless the global exception policy is deliberately
	expanded. Evidence: `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:32-55`,
	`WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:36-57`, and
	`WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:26-39`.

### Projects, packages, and solution membership

* The API targets `net10.0` with nullable and implicit usings enabled. Direct package
	versions are Aspire EF SQL Server 13.5.3, AutoMapper 16.2.0, FluentValidation 12.1.1,
	MediatR 14.2.0, Reprise 3.7.0, Serilog.AspNetCore 10.0.0, and Swashbuckle 10.2.3.
	Evidence: `WebApiMediatorCQRS/WebApiMediatorCQRS.csproj:1-22`.
* Resolved assets use EF Core, EF Core Relational, and EF Core SQL Server 10.0.11.
	Evidence: `WebApiMediatorCQRS/obj/project.assets.json:316-378` and
	`WebApiMediatorCQRS/obj/project.assets.json:1703-1772`.
* The AppHost uses `Aspire.AppHost.Sdk/13.5.3`, targets `net10.0`, and references the API.
	Evidence: `AspireAppHost/AspireAppHost.AppHost/AspireAppHost.AppHost.csproj:1-13`.
* The solution includes API, AppHost, ServiceDefaults, and tests, with Debug and Release
	build mappings for each. Evidence: `WebApiMediatorCQRS.sln:6-19` and
	`WebApiMediatorCQRS.sln:21-46`.

### Current tests and capabilities

* The test project targets `net10.0`, enables Microsoft Testing Platform runner support,
	and uses Aspire.Hosting.Testing 13.4.6, coverlet 6.0.4, Microsoft.NET.Test.Sdk 17.14.1,
	xUnit runner 3.1.4, and xUnit v3 3.0.1. It references AppHost, not the API directly.
	Evidence: `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:1-31`.
* Aspire.Hosting.Testing 13.4.6 is behind the AppHost SDK and runtime integration version
	13.5.3. The solution currently compiles, but keeping these aligned reduces future API or
	generated-project compatibility risk.
* The sole integration test starts the full AppHost, waits up to 30 seconds for the API
	resource to become healthy, creates its service-discovered HTTP client, requests
	`/swagger`, and asserts 200. Evidence: `WebApiMediatorCQRS.Tests/IntegrationTests.cs:5-39`.
* There is no Supplier or product CRUD integration coverage, database reset/seed fixture,
	test-specific connection override, direct handler test infrastructure, or asserted
	Problem Details body. The current test proves startup and Swagger availability only.
* `AGENTS.md` is stale where it says no automated test project exists. The solution and test
	project contradict that statement. Evidence: `AGENTS.md:36-40`,
	`WebApiMediatorCQRS.sln:18-19`, and
	`WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:1-31`.

### Build and run constraints

* On 2026-09-06, `dotnet --version` reported SDK `10.0.400`. A repository-root
	`dotnet build WebApiMediatorCQRS.sln --no-restore` succeeded for all four projects in
	11.6 seconds with one warning and no errors.
* The warning is `ASPIRE010`: AppHost resolves `AspireUseCliBundle=false`, so some Aspire
	features need the CLI bundle enabled. This matches repository guidance. Evidence:
	`AGENTS.md:42-44`.
* AppHost declares only the API resource named `webapimediatorcqrs`; it does not provision
	SQL Server or Northwind. Evidence: `AspireAppHost/AspireAppHost.AppHost/AppHost.cs:1-5`.
* Database-backed routes require an externally reachable `Northwind` database on Windows
	LocalDB instance `(localdb)\sql2025` with integrated security. The repository does not
	create, migrate, or seed it. Evidence: `WebApiMediatorCQRS/appsettings.json:8-11` and
	`AGENTS.md:46-48`.
* Direct API launch profiles use HTTP `5039` and HTTPS `7181`; Swagger and Aspire health
	endpoints depend on Development environment. Evidence:
	`WebApiMediatorCQRS/Properties/launchSettings.json:13-30`,
	`WebApiMediatorCQRS/Program.cs:68-75`, and
	`AspireAppHost/AspireAppHost.ServiceDefaults/Extensions.cs:109-124`.
* Repository guidance requires restore/build from the root, then either AppHost or direct
	API run, and recommends exercising database-backed routes against reachable Northwind.
	Evidence: `AGENTS.md:23-48` and `AGENTS.md:86-93`.
* The application and tests were not started during this research. Runtime database
	connectivity, actual identity metadata, FK action in SQL Server, and CRUD HTTP behavior
	therefore remain unverified.

## Evidence

Primary evidence is local repository source and configuration at the paths and line ranges
listed under Findings. No external documentation was needed. The build observation used the
repository root and existing restored assets; it did not alter application source.

## Remaining Gaps

* Actual SQL Server metadata for `dbo.Suppliers.SupplierID`, including `IDENTITY` status
* Actual `FK_Products_Suppliers` delete action in the connected Northwind database
* Availability of `(localdb)\sql2025` and the `Northwind` catalog on the execution machine
* Desired Supplier delete policy: conflict, null dependent FKs, or database-level cascade
* Desired update semantics for omitted optional properties: full replacement or partial patch
* Runtime shape and status of Problem Details for SQL constraint and validation failures
* Whether Aspire.Hosting.Testing should be upgraded from 13.4.6 to 13.5.3 before adding tests

## Recommended Next Research

* Query SQL Server catalog views for Supplier key generation, column nullability and lengths,
	and `FK_Products_Suppliers` delete action against the configured Northwind database.
* Run the existing Aspire integration test to establish runtime health and LocalDB reachability.
* Exercise representative Supplier create, update, missing-row delete, referenced-row delete,
	and concurrent-reference cases once endpoints exist.
* Confirm the intended Supplier deletion and update contracts with the API owner before
	implementing command statuses and HTTP results.
* Reconcile `AGENTS.md` with the existing test project and align Aspire test/runtime versions.

## Clarifying Questions

* Should deleting a Supplier referenced by Products return 409, or should it set those
	`Products.SupplierId` values to null?
* Should update use PUT replacement semantics or PATCH-style preservation of omitted values?
