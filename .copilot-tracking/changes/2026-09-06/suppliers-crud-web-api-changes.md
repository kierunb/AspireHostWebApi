<!-- markdownlint-disable-file -->
# Release Changes: Suppliers CRUD Web API

**Related Plan**: `suppliers-crud-web-api-plan.instructions.md`
**Implementation Date**: 2026-09-06

## Summary

Implemented a Product-style Supplier CRUD vertical slice with schema-derived validation, deterministic projected reads, guarded deletion, executable HTTP examples, and database-free validator, mapping, and OpenAPI coverage.

## Changes

### Added

* `WebApiMediatorCQRS/ApiModels/SupplierModels.cs` - Added immutable Supplier response, create, and replacement request contracts
* `WebApiMediatorCQRS/Commands/SupplierCommands.cs` - Added create, replacement, and guarded delete commands, validators, handlers, and mutation outcomes
* `WebApiMediatorCQRS/Endpoints/CreateSupplierEndpoint.cs` - Added validated `POST /suppliers` with `201 Created` and resource location
* `WebApiMediatorCQRS/Endpoints/DeleteSupplierEndpoint.cs` - Added validated `DELETE /suppliers/{id:int}` with `204`, `404`, and `409` outcomes
* `WebApiMediatorCQRS/Endpoints/GetSuppliersEndpoint.cs` - Added Supplier collection and item `GET` operations
* `WebApiMediatorCQRS/Endpoints/UpdateSupplierEndpoint.cs` - Added validated replacement `PUT /suppliers/{id:int}`
* `WebApiMediatorCQRS/Profiles/SupplierProfile.cs` - Added Supplier entity-to-response AutoMapper configuration
* `WebApiMediatorCQRS/Queries/SupplierQueries.cs` - Added ordered list and by-ID projected queries with ID validation
* `WebApiMediatorCQRS/Suppliers.http` - Added CRUD, invalid-input, missing-row, null-clearing, and delete-conflict examples with an isolated-database warning
* `WebApiMediatorCQRS.Tests/SupplierIntegrationTests.cs` - Added Aspire-hosted OpenAPI assertions for two paths, five operations, and documented statuses
* `WebApiMediatorCQRS.Tests/SupplierProfileTests.cs` - Added AutoMapper configuration, scalar mapping, and response-contract tests
* `WebApiMediatorCQRS.Tests/SupplierValidatorTests.cs` - Added database-free ID, required-field, length-boundary, nullable-field, and HomePage validation tests

### Modified

* `AGENTS.md` - Corrected test-project guidance and retained isolated-database safety requirements
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` - Added a direct project reference to the API for fast Supplier tests

### Removed

* None

## Additional or Deviating Changes

* Phase 1 validation succeeded with `dotnet build WebApiMediatorCQRS/WebApiMediatorCQRS.csproj`
* Phase 2 validation succeeded with `dotnet build WebApiMediatorCQRS/WebApiMediatorCQRS.csproj`
* Supplier delete mirrors the Product handler's broad `DbUpdateException` race fallback as documented by DD-01
* Phase 3 API validation succeeded with zero warnings and zero errors
* SQL mutation automation remains deferred until disposable Northwind infrastructure exists, as documented by DD-02
* Phase 4 solution build succeeded with only the pre-existing `ASPIRE010` warning
* Phase 4 test run passed 39 tests with zero failures and zero skipped tests
* Aspire package alignment remains deferred as documented by DD-04
* Final validation passed with `dotnet build WebApiMediatorCQRS.sln` and 39 passing tests
* SQL-backed CRUD and metadata verification was not executed because no isolated Northwind-compatible database was configured; the persistent default LocalDB was intentionally left untouched

## Release Summary

Supplier CRUD now exposes five Reprise operations over `/suppliers` and `/suppliers/{id:int}`. The implementation adds nine production artifacts for contracts, mapping, CQRS handlers, endpoints, and HTTP examples; three test files cover validators, mapping, response shape, endpoint discovery, operations, and documented statuses. The test project now directly references the API, and repository guidance reflects the existing xUnit v3 Microsoft Testing Platform setup.

No dependency package, generated EF Core file, middleware registration, or infrastructure resource changed. The full solution builds and all 39 tests pass. Runtime SQL identity, foreign-key behavior, and destructive CRUD scenarios remain deferred until disposable Northwind infrastructure is supplied. The existing `ASPIRE010` warning remains unchanged.