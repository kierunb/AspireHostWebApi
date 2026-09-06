<!-- markdownlint-disable-file -->
# Implementation Details: Suppliers CRUD Web API

## Context Reference

Sources: `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md`, `.copilot-tracking/research/subagents/2026-09-06/suppliers-crud-plan-readiness-research.md`, `AGENTS.md`, and the verified Product CRUD vertical slice.

## Implementation Phase 1: Supplier Contracts and Mapping

<!-- parallelizable: false -->

### Step 1.1: Create Supplier API contracts

Create immutable records for the response, create request, and replacement request. Expose all Supplier scalar fields, keep `SupplierId` server-controlled, preserve database nullability, and omit the `Products` navigation property.

Files:
* `WebApiMediatorCQRS/ApiModels/SupplierModels.cs` - Supplier response and write contracts

Discrepancy references:
* None. This step follows the selected Product-style vertical slice.

Success criteria:
* `SupplierResponse` exposes `SupplierId` and every Supplier scalar column
* Create and update requests exclude `SupplierId` and navigation properties
* Nullable annotations match the generated EF model

Context references:
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 151-195) - Proposed contracts
* `WebApiMediatorCQRS/ApiModels/ProductModels.cs` (Lines 3-41) - Local contract pattern

Dependencies:
* Existing `WebApiMediatorCQRS` API project

### Step 1.2: Create the Supplier mapping profile

Add entity-to-response mapping for SQL projection and tracked-write responses. Keep mapping configuration limited to `Suppliers` to `SupplierResponse`; handlers should assign writable entity fields explicitly.

Files:
* `WebApiMediatorCQRS/Profiles/SupplierProfile.cs` - AutoMapper profile

Success criteria:
* AutoMapper configuration maps all Supplier scalar fields to `SupplierResponse`
* The profile can be discovered by the existing assembly scan
* No generated database file is changed

Context references:
* `WebApiMediatorCQRS/Profiles/ProductProfile.cs` (Lines 7-14) - Local profile pattern
* `WebApiMediatorCQRS/Program.cs` (Lines 50-55) - AutoMapper assembly scanning

Dependencies:
* Step 1.1 completion

### Step 1.3: Validate phase changes

Build the API project to verify contract nullability, mapping references, namespaces, and assembly discovery types.

Validation commands:
* `dotnet build WebApiMediatorCQRS/WebApiMediatorCQRS.csproj` - API compile validation

## Implementation Phase 2: Supplier CQRS Operations

<!-- parallelizable: true -->

### Step 2.1: Implement Supplier read queries

Add list and by-ID MediatR queries, an ID validator, and handlers. The list handler must use `AsNoTracking`, order by `SupplierId`, apply `ProjectTo<SupplierResponse>` after filtering and ordering, and materialize asynchronously with the incoming cancellation token. The item query returns `null` when no row matches.

Files:
* `WebApiMediatorCQRS/Queries/SupplierQueries.cs` - List and item queries, validator, and handlers

Discrepancy references:
* None. Query behavior follows the verified Product precedent.

Success criteria:
* `GET` query handlers do not track Supplier entities
* List results are deterministic by ascending `SupplierId`
* Item IDs must be greater than zero
* Every EF Core terminal operation receives the cancellation token

Context references:
* `WebApiMediatorCQRS/Queries/ProductQueries.cs` (Lines 11-53) - Query and projection pattern
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 197-204) - Required query shape

Dependencies:
* Implementation Phase 1 completion

### Step 2.2: Implement Supplier mutation commands

Add create, update, and delete commands with colocated FluentValidation validators, handlers, and a Supplier mutation status enum or equivalent local result type. Create saves a new entity before mapping its store-generated key. Update performs full replacement of every writable scalar and returns not found without upsert. Delete checks for related Products before removal, catches `DbUpdateException` as a race fallback, and reports conflict without deleting dependents.

Files:
* `WebApiMediatorCQRS/Commands/SupplierCommands.cs` - Commands, validators, mutation outcomes, and handlers

Discrepancy references:
* `DD-01` - Broad `DbUpdateException` handling temporarily mirrors Product CRUD pending provider-specific evidence

Success criteria:
* Validators enforce positive route IDs, required `CompanyName`, and every schema-derived maximum length
* `HomePage` remains optional without an invented format or length policy
* Update assigns every writable Supplier scalar, including nullable fields
* Delete returns conflict when a Product references the Supplier
* All asynchronous EF Core operations propagate cancellation

Context references:
* `WebApiMediatorCQRS/Commands/ProductCommands.cs` (Lines 10-248) - Mutation and validator pattern
* `WebApiMediatorCQRS/Database/NorthwindContext.cs` (Lines 319-337) - Supplier constraints
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 207-230) - Delete flow and exception fallback

Dependencies:
* Implementation Phase 1 completion

### Step 2.3: Validate phase changes

Build the API after both parallel work items are complete because they share the API compilation scope.

Validation commands:
* `dotnet build WebApiMediatorCQRS/WebApiMediatorCQRS.csproj` - CQRS compile validation

## Implementation Phase 3: Supplier HTTP Surface

<!-- parallelizable: true -->

### Step 3.1: Implement Supplier read endpoints

Create one Reprise endpoint class for collection and item reads. Map `GET /suppliers` to an ordered `200` response. Map `GET /suppliers/{id:int}` to `200`, validation problem `400`, or `404`. Resolve and execute the query validator explicitly because pipeline validation is disabled.

Files:
* `WebApiMediatorCQRS/Endpoints/GetSuppliersEndpoint.cs` - Supplier read operations

Success criteria:
* Both read operations use the established Reprise attributes
* Item validation runs before `mediator.Send`
* Endpoint results preserve the documented status contract

Context references:
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs` (Lines 8-45) - Read endpoint pattern
* `WebApiMediatorCQRS/Program.cs` (Lines 38-47) - Enabled MediatR pipeline behavior

Dependencies:
* Implementation Phase 2 completion

### Step 3.2: Implement Supplier mutation endpoints

Create Reprise endpoint classes for `POST`, replacement `PUT`, and `DELETE`. Each endpoint explicitly validates its command, passes the cancellation token to MediatR, and translates only Supplier domain outcomes into HTTP results. Create returns `201` with the response body and `/suppliers/{id}` location. Update returns `200` or `404`. Delete returns `204`, `404`, or Supplier-specific `409`.

Files:
* `WebApiMediatorCQRS/Endpoints/CreateSupplierEndpoint.cs` - Create operation
* `WebApiMediatorCQRS/Endpoints/UpdateSupplierEndpoint.cs` - Replacement operation
* `WebApiMediatorCQRS/Endpoints/DeleteSupplierEndpoint.cs` - Delete operation

Success criteria:
* Invalid requests return validation problem `400` before handler execution
* Create emits the assigned identifier in both body and `Location`
* Update does not create missing Suppliers
* Delete does not unlink or cascade-delete Products

Context references:
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs` (Lines 9-76) - Create result translation
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs` (Lines 9-56) - Replacement result translation
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs` (Lines 8-37) - Delete result translation

Dependencies:
* Implementation Phase 2 completion

### Step 3.3: Add executable HTTP examples

Add direct-API requests for list, item lookup, create, full replacement with nullable-field clearing, delete, invalid ID, invalid company name, missing Supplier, and delete conflict. Store created IDs as manually replaceable variables and warn against executing mutations against a non-isolated database.

Files:
* `WebApiMediatorCQRS/Suppliers.http` - Supplier CRUD smoke-test requests

Discrepancy references:
* `DD-02` - Automated SQL mutation coverage remains deferred until database isolation is defined

Success criteria:
* The file targets `http://localhost:5039`
* Examples cover every expected status family and the null-clearing update behavior
* Mutation examples identify the isolated-database prerequisite

Context references:
* `WebApiMediatorCQRS/Products.http` (Lines 1-143) - Existing HTTP request convention
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 251-274) - Proposed host and create request

Dependencies:
* Steps 3.1 and 3.2 completion

### Step 3.4: Validate phase changes

Build the API project after all endpoint classes are present. Endpoint discovery is verified in Phase 4 through the generated OpenAPI document.

Validation commands:
* `dotnet build WebApiMediatorCQRS/WebApiMediatorCQRS.csproj` - Endpoint compile validation

## Implementation Phase 4: Automated Tests and Repository Guidance

<!-- parallelizable: false -->

### Step 4.1: Enable direct API tests

Add a direct project reference from the xUnit v3 test project to the API project. Preserve the existing Microsoft Testing Platform bridge and package versions. Do not add a mocking package because the planned validator and profile tests require no substitutes. Do not align the Aspire testing package as part of this feature.

Files:
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` - API project reference

Discrepancy references:
* `DD-04` - Aspire package version alignment remains outside feature scope

Success criteria:
* Test sources can reference Supplier validators, contracts, entities, and mapping profiles
* Existing AppHost project reference and test runner settings remain intact
* No unnecessary package is added

Context references:
* `.copilot-tracking/research/subagents/2026-09-06/suppliers-crud-plan-readiness-research.md` (Lines 89-121) - Test project constraints

Dependencies:
* Implementation Phases 1 through 3 completion

### Step 4.2: Add Supplier validator tests

Create database-free xUnit v3 tests for positive IDs, missing and blank `CompanyName`, exact maximum lengths, over-limit values, optional null fields, and unrestricted optional `HomePage`. Use theories for sibling boundaries and names in `GivenContext_WhenAction_ExpectedResult` form.

Files:
* `WebApiMediatorCQRS.Tests/SupplierValidatorTests.cs` - Supplier validation behavior

Success criteria:
* Every Supplier validator has direct valid and invalid evidence
* Every bounded field is tested at and immediately above its limit
* Tests require no database or application host

Context references:
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 232-247) - Complete validation matrix
* C# test instructions - xUnit naming and theory conventions

Dependencies:
* Step 4.1 completion

### Step 4.3: Add Supplier mapping tests

Build an AutoMapper configuration containing `SupplierProfile`, assert configuration validity, map a fully populated Supplier entity, and compare every scalar response value. Verify the public response contract has no navigation member.

Files:
* `WebApiMediatorCQRS.Tests/SupplierProfileTests.cs` - Mapping configuration and scalar mapping tests

Success criteria:
* AutoMapper configuration validation passes
* Every scalar field is asserted in the mapped response
* Navigation data does not leak into the API contract

Context references:
* `WebApiMediatorCQRS/Profiles/ProductProfile.cs` (Lines 7-14) - Mapping profile precedent
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 383-386) - Mapping validation requirements

Dependencies:
* Step 4.1 completion

### Step 4.4: Add Supplier OpenAPI contract tests

Extend the Aspire HTTP test coverage in a dedicated Supplier test class. Start the AppHost in Development, wait for the API resource, fetch `/swagger/v1/swagger.json`, parse it with `JsonDocument`, and assert two Supplier path keys containing five operations: `GET` and `POST` on `/suppliers`, plus `GET`, `PUT`, and `DELETE` on `/suppliers/{id}`. Assert documented success and error status codes without invoking database operations.

Files:
* `WebApiMediatorCQRS.Tests/SupplierIntegrationTests.cs` - OpenAPI route and status contract test

Discrepancy references:
* `DD-02` - This test verifies discovery and HTTP metadata, not database mutations
* `DD-03` - The assertion uses five operations over two path templates

Success criteria:
* Endpoint discovery is proven on the current .NET 10 and Reprise versions
* Assertions distinguish five operations from two path templates
* The test does not require Northwind connectivity

Context references:
* `WebApiMediatorCQRS.Tests/IntegrationTests.cs` (Lines 14-37) - Existing Aspire test harness
* `.copilot-tracking/research/subagents/2026-09-06/suppliers-crud-plan-readiness-research.md` (Lines 157-161) - OpenAPI correction and validation method

Dependencies:
* Step 4.1 completion
* Implementation Phase 3 completion

### Step 4.5: Correct repository test guidance

Update repository instructions to state that the xUnit v3 test project exists, is included in the solution, and runs through the configured Microsoft Testing Platform bridge. Retain the isolated-database warning for database-backed tests.

Files:
* `AGENTS.md` - Build, test, and database verification guidance

Success criteria:
* Instructions no longer claim that the repository has no test project
* The documented test command matches the current project configuration
* Database mutation tests are not presented as safe against the default LocalDB

Context references:
* `AGENTS.md` (Lines 38-40) - Stale statement
* `.copilot-tracking/research/subagents/2026-09-06/suppliers-crud-plan-readiness-research.md` (Lines 135-142) - Verified test project and command

Dependencies:
* Step 4.1 completion

### Step 4.6: Validate phase changes

Build and run the complete test project. The repository uses SDK 10 VSTest command mode bridged to Microsoft Testing Platform, so the project path is positional. No filter is needed for the complete suite.

Validation commands:
* `dotnet build WebApiMediatorCQRS.sln` - Full compile validation
* `dotnet test WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` - Complete xUnit v3 suite through the configured MTP bridge

## Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

### Step 5.1: Run full project validation

Execute the repository-wide build and complete test suite from the repository root:
* `dotnet build WebApiMediatorCQRS.sln`
* `dotnet test WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj`

Start the direct API only when an isolated Northwind-compatible connection is available:
* Set the `ConnectionStrings__NorthwindDB` override to the isolated database
* Run `dotnet run --project WebApiMediatorCQRS/WebApiMediatorCQRS.csproj --launch-profile http`
* Execute the non-destructive and CRUD scenarios in `WebApiMediatorCQRS/Suppliers.http`

### Step 5.2: Verify runtime database assumptions when available

Inspect `dbo.Suppliers.SupplierID` identity metadata and `FK_Products_Suppliers` delete behavior on the isolated target. Verify create assigns a key, update clears nullable fields, referenced delete returns `409`, unreferenced delete returns `204`, and missing reads return `404`. Record any mismatch before narrowing exception handling.

Discrepancy references:
* `DD-01` - Provider-specific delete classification awaits observed SQL Server behavior
* `DD-02` - SQL CRUD automation requires a separately planned disposable database lifecycle

Context references:
* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` (Lines 395-403) - Unresolved runtime facts

### Step 5.3: Fix minor validation issues

Correct isolated compile, analyzer, mapping, OpenAPI assertion, or test failures introduced by the Supplier slice. Re-run the narrow failing command, then repeat the full build and test suite.

### Step 5.4: Report blocking issues

Document failures caused by unavailable SQL Server, incompatible Reprise runtime behavior, or schema differences. Do not rewrite generated EF files, enable global validation, or redesign database orchestration during this phase; route those changes to follow-on research and planning.

## Dependencies

* .NET 10 SDK
* Reachable NuGet feeds for existing packages
* Existing Northwind EF Core model
* Isolated Northwind-compatible SQL Server only for manual mutation verification

## Success Criteria

* Supplier CRUD compiles and is discovered as five HTTP operations over two path templates
* Database-free validator and mapping tests pass
* OpenAPI contract tests pass without Northwind connectivity
* Full solution build and existing tests remain successful
* SQL-backed behavior is either verified against an isolated database or reported as an explicit runtime blocker