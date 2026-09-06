---
title: Suppliers CRUD Plan Readiness Research
description: Verification of the Suppliers CRUD primary research against the current workspace
ms.date: 2026-09-06
ms.topic: concept
---
<!-- markdownlint-disable-file -->

## Research Scope

Assess `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md`
for implementation-planning readiness. Verify its architectural claims, exact target
files, registration and discovery assumptions, test constraints, and build and route
validation commands against the current workspace. Do not modify production code.

## Readiness Status

Research status: Complete.

Planning readiness: Conditionally ready. The production architecture, Supplier
contract, and proposed production file boundaries are sufficiently grounded to begin
implementation. The primary document is not fully execution-ready until it adds the
test-project change, names the focused test files, defines the database-test opt-in
and cleanup strategy, corrects the OpenAPI route count, and includes the stale
`AGENTS.md` update.

## Verified Findings

### Production Architecture

* The Product vertical slice is the correct local precedent. It defines separate
  create, update, and response records in
  `WebApiMediatorCQRS/ApiModels/ProductModels.cs:3-41`; projected, no-tracking reads
  in `WebApiMediatorCQRS/Queries/ProductQueries.cs:11-53`; colocated commands,
  validators, handlers, and mutation outcomes in
  `WebApiMediatorCQRS/Commands/ProductCommands.cs:10-248`; and a profile in
  `WebApiMediatorCQRS/Profiles/ProductProfile.cs:7-14`.
* The Product HTTP surface has five operations over two path templates, not five
  distinct routes. The operations are declared in
  `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs:11-45`,
  `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs:12-55`,
  `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs:12-56`, and
  `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs:11-37`.
* Endpoint-local validation is required under the current configuration.
  `WebApiMediatorCQRS/Program.cs:38-44` enables only `LoggingBehavior<,>` and leaves
  `ValidationBehavior<,>` commented out. Product item, create, update, and delete
  endpoints therefore resolve `IValidator<T>` and call `ValidateAsync` before
  `IMediator.Send`.
* Assembly discovery claims are correct. `WebApiMediatorCQRS/Program.cs:8` selects
  the API assembly; lines 38-55 scan it for MediatR handlers, FluentValidation
  validators, AutoMapper profiles, and Reprise services; line 80 maps discovered
  endpoints. Supplier types placed in the proposed API folders need no explicit
  registration and no `Program.cs` edit.
* The Supplier persistence constraints in the primary document match
  `WebApiMediatorCQRS/Database/NorthwindContext.cs:319-337`: required `CompanyName`
  with maximum 40, the stated nullable bounded text fields, and unbounded `ntext`
  `HomePage`. `WebApiMediatorCQRS/Database/Suppliers.cs:1-35` is marked generated,
  so excluding database-entity edits is correct.
* The delete conflict surface is real. `WebApiMediatorCQRS/Database/Products.cs:14`
  makes `SupplierId` nullable, while
  `WebApiMediatorCQRS/Database/NorthwindContext.cs:288-290` configures
  `FK_Products_Suppliers`. A pre-delete `Products.AnyAsync` check follows the
  existing Product delete style, but deployed FK behavior remains a runtime fact.

### Target Files

The following production targets in the primary document are exact and consistent
with the Product slice:

* `WebApiMediatorCQRS/ApiModels/SupplierModels.cs`
* `WebApiMediatorCQRS/Commands/SupplierCommands.cs`
* `WebApiMediatorCQRS/Endpoints/CreateSupplierEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/DeleteSupplierEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/GetSuppliersEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/UpdateSupplierEndpoint.cs`
* `WebApiMediatorCQRS/Profiles/SupplierProfile.cs`
* `WebApiMediatorCQRS/Queries/SupplierQueries.cs`
* `WebApiMediatorCQRS/Suppliers.http`

The test and documentation targets are incomplete. An implementation plan should
also name:

* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj`
* `WebApiMediatorCQRS.Tests/SupplierValidatorTests.cs`
* `WebApiMediatorCQRS.Tests/SupplierProfileTests.cs`
* `WebApiMediatorCQRS.Tests/SupplierIntegrationTests.cs`
* `AGENTS.md`

### Test Project And Constraints

* `AGENTS.md:38-40` incorrectly states that no automated test project exists.
  `WebApiMediatorCQRS.sln:18-19` includes
  `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` and solution build
  configuration includes it.
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:4-18` is a .NET 10 xUnit
  v3 project using Microsoft Testing Platform. Lines 9-10 enable the MTP runner and
  `dotnet test` bridge support.
* The test project references only the AppHost at
  `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:22`. The AppHost's
  project resource reference at
  `AspireAppHost/AspireAppHost.AppHost/AspireAppHost.AppHost.csproj:12` supports
  orchestration and generated `Projects.*` metadata, but it does not provide the
  direct API compile reference needed by validator and mapping tests. Add a direct
  `ProjectReference` to `WebApiMediatorCQRS/WebApiMediatorCQRS.csproj`.
* NSubstitute is not currently referenced. It is not needed for direct validator or
  AutoMapper configuration tests, so the plan should avoid adding it unless a chosen
  test design actually mocks dependencies.
* The only current test starts the AppHost, waits for the API resource, and checks
  `GET /swagger`; see `WebApiMediatorCQRS.Tests/IntegrationTests.cs:14-37`. It does
  not validate OpenAPI paths, any CRUD route, database connectivity, or Supplier
  behavior.
* `AspireAppHost/AspireAppHost.AppHost/AppHost.cs:1-5` provisions only the API
  project. It does not provision SQL Server or initialize Northwind. The primary
  document correctly rejects destructive integration tests against the default
  developer database, but “gated” tests are not implementable until the gate,
  connection injection, seed ownership, and cleanup rules are specified.
* There is a nonblocking Aspire version skew:
  `AspireAppHost/AspireAppHost.AppHost/AspireAppHost.AppHost.csproj:1` uses 13.5.3,
  while `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj:14` uses
  `Aspire.Hosting.Testing` 13.4.6. The existing test passes, but the implementation
  plan should align these versions or explicitly accept the mismatch.

### Commands And Route Validation

These checks were executed from the repository root on 2026-09-06:

```powershell
dotnet build WebApiMediatorCQRS.sln
```

The build succeeded for the API, ServiceDefaults, AppHost, and test project. It
emitted the already documented `ASPIRE010` warning because the AppHost has
`AspireUseCliBundle=false`.

The focused existing `IntegrationTests.cs` test also passed: one passed, zero failed.
The repository has no `global.json`, so the current SDK uses `dotnet test` VSTest
command mode with the configured MTP bridge. The reproducible full-project command
is:

```powershell
dotnet test WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj
```

For a manual direct-API run, make the launch profile explicit because
`WebApiMediatorCQRS/Properties/launchSettings.json:13-20` owns the Development
environment and `http://localhost:5039` binding:

```powershell
dotnet run --project WebApiMediatorCQRS/WebApiMediatorCQRS.csproj --launch-profile http
```

`WebApiMediatorCQRS/Suppliers.http` can then target `http://localhost:5039`, but
database-backed operations require the LocalDB connection from
`WebApiMediatorCQRS/appsettings.json:9-10` or an overridden `NorthwindDB`
connection.

Prefer an automated OpenAPI assertion through the existing Aspire harness. Fetch
`/swagger/v1/swagger.json`, then assert two Supplier path keys and five operations:
`GET` and `POST` on `/suppliers`, plus `GET`, `PUT`, and `DELETE` on
`/suppliers/{id}`. The primary document's statements at lines 393 and 409 that five
routes should appear are inaccurate.

## Risks And Missing Details

* Fast tests cannot compile as proposed until the test project directly references
  the API project.
* The primary document names only `SupplierIntegrationTests.cs` in its target tree
  at line 129, while lines 347-348 also require validator and mapping tests. Their
  exact files and the test-project edit are missing from the implementation list.
* Running all integration tests against the default connection can mutate the
  persistent Northwind LocalDB database. The test plan currently lacks an explicit
  opt-in variable, isolated connection source, deterministic seed data, and cleanup
  behavior.
* A broad `DbUpdateException` catch, copied from Product delete, can misclassify
  unrelated database failures as `409 Conflict`. The primary document records this
  concern but leaves exception classification unresolved.
* Swagger is Development-only at `WebApiMediatorCQRS/Program.cs:68-76`. OpenAPI
  route validation must run with the Development environment, as the current Aspire
  test does, or it will receive no Swagger document.
* The actual SQL Server `IDENTITY` property and deployed FK delete action are not
  proven by the generated EF model. These do not block code structure, but they do
  block claiming complete persistence verification.

## Recommendations

1. Keep the proposed nine production artifacts and leave generated database files
   and `Program.cs` unchanged.
2. Add `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` to the plan and add
   a direct API project reference. Align `Aspire.Hosting.Testing` with AppHost 13.5.3
   unless compatibility policy says otherwise.
3. Name separate validator, profile, and integration test files. Keep validator and
   profile tests database-free and runnable by default.
4. Define database integration tests as explicit opt-in tests against an overridden,
   disposable Northwind-compatible database. Specify environment variable, seed,
   ownership, cleanup, and parallelization behavior before implementation.
5. Extend the Aspire test harness to inspect `/swagger/v1/swagger.json`; assert five
   operations over two path templates and the declared success and error statuses.
6. Update `AGENTS.md:38-40` to document the real test project and verified test
   command. This prevents implementation agents from skipping tests based on stale
   repository guidance.
7. Retain the primary document's runtime checks for Supplier identity generation,
   FK delete behavior, and provider-specific delete exceptions.

## Remaining Questions

* What opt-in variable and isolated SQL Server lifecycle should Supplier mutation
  integration tests use?
* Who owns seed records and cleanup when testing the referenced-Supplier `409`
  scenario, especially if tests run concurrently?
* Should `Aspire.Hosting.Testing` be upgraded from 13.4.6 to 13.5.3 as part of this
  feature, or is the current version skew intentional?
* Does the reachable Northwind database confirm `Suppliers.SupplierID` as
  `IDENTITY`, and what is the deployed `FK_Products_Suppliers` delete action?
* Should database integration tests run in the normal test command, a trait-filtered
  command, or only in a dedicated CI job?

## Evidence Reviewed

* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md`
* `AGENTS.md`
* `WebApiMediatorCQRS.sln`
* `AspireAppHost/AspireAppHost.AppHost/AppHost.cs`
* `AspireAppHost/AspireAppHost.AppHost/AspireAppHost.AppHost.csproj`
* `WebApiMediatorCQRS/Program.cs`
* `WebApiMediatorCQRS/WebApiMediatorCQRS.csproj`
* `WebApiMediatorCQRS/Properties/launchSettings.json`
* `WebApiMediatorCQRS/appsettings.json`
* `WebApiMediatorCQRS/ApiModels/ProductModels.cs`
* `WebApiMediatorCQRS/Commands/ProductCommands.cs`
* `WebApiMediatorCQRS/Queries/ProductQueries.cs`
* `WebApiMediatorCQRS/Profiles/ProductProfile.cs`
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs`
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs`
* `WebApiMediatorCQRS/Database/Suppliers.cs`
* `WebApiMediatorCQRS/Database/Products.cs`
* `WebApiMediatorCQRS/Database/NorthwindContext.cs`
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj`
* `WebApiMediatorCQRS.Tests/IntegrationTests.cs`