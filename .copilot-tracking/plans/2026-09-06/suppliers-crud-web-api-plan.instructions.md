---
applyTo: '.copilot-tracking/changes/2026-09-06/suppliers-crud-web-api-changes.md'
---
<!-- markdownlint-disable-file -->
# Implementation Plan: Suppliers CRUD Web API

## Overview

Implement complete Supplier CRUD as a Product-style Reprise and MediatR vertical slice, with schema-derived validation, deterministic projected reads, guarded deletion, focused automated tests, and isolated-database verification guidance.

## Objectives

### User Requirements

* Plan implementation tasks from the supplied Suppliers CRUD research document - Source: user request and `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md`
* Add create, read, update, and delete HTTP operations for Suppliers - Source: primary research task implementation requests
* Preserve ASP.NET Core, MediatR CQRS, Reprise, AutoMapper, FluentValidation, and EF Core conventions - Source: primary research task implementation requests
* Define focused validation and verification steps - Source: primary research task implementation requests

### Derived Objectives

* Keep generated Supplier entity and DbContext files unchanged - Derived from: EF Core Power Tools generation markers and repository guidance
* Add a direct API reference to the existing xUnit v3 test project - Derived from: plan-readiness research showing the current AppHost reference does not expose API types
* Verify five Supplier operations over two OpenAPI path templates without requiring database access - Derived from: current Aspire test harness and corrected route counting
* Defer destructive automated SQL tests until a disposable Northwind lifecycle exists - Derived from: persistent LocalDB risk and missing seed and cleanup ownership
* Correct stale `AGENTS.md` test guidance - Derived from: the test project is present in the solution and has a verified command

## Context Summary

### Project Files

* `WebApiMediatorCQRS/ApiModels/ProductModels.cs` - Request and response contract precedent
* `WebApiMediatorCQRS/Commands/ProductCommands.cs` - Mutation, validation, and handler precedent
* `WebApiMediatorCQRS/Queries/ProductQueries.cs` - No-tracking projection precedent
* `WebApiMediatorCQRS/Endpoints/GetProductsEndpoint.cs` - Collection and item read precedent
* `WebApiMediatorCQRS/Endpoints/CreateProductEndpoint.cs` - Created response precedent
* `WebApiMediatorCQRS/Endpoints/UpdateProductEndpoint.cs` - Full replacement precedent
* `WebApiMediatorCQRS/Endpoints/DeleteProductEndpoint.cs` - Guarded deletion precedent
* `WebApiMediatorCQRS/Database/Suppliers.cs` - Generated Supplier persistence entity, read-only for this task
* `WebApiMediatorCQRS/Database/NorthwindContext.cs` - Supplier schema constraints and Product relationship
* `WebApiMediatorCQRS/Program.cs` - Existing assembly scanning and disabled validation pipeline
* `WebApiMediatorCQRS.Tests/IntegrationTests.cs` - Existing Aspire test harness
* `WebApiMediatorCQRS.Tests/WebApiMediatorCQRS.Tests.csproj` - xUnit v3 and Microsoft Testing Platform configuration

### References

* `.copilot-tracking/research/2026-09-06/suppliers-crud-web-api-research.md` - Primary architecture, contract, alternatives, and validation research
* `.copilot-tracking/research/subagents/2026-09-06/suppliers-crud-plan-readiness-research.md` - Current-workspace verification and corrected test constraints
* `AGENTS.md` - Repository build, architecture, and generated-code guidance
* `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0` - HTTP response semantics
* `https://learn.microsoft.com/en-us/ef/core/miscellaneous/async` - EF Core cancellation and async behavior
* `https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying` - Read projection guidance

### Standards References

* `c:/Users/bkierun/.vscode/extensions/ise-hve-essentials.hve-core-all-3.2.2/.github/instructions/coding-standards/csharp/csharp.instructions.md` - C# 14 and .NET 10 conventions
* `c:/Users/bkierun/.vscode/extensions/ise-hve-essentials.hve-core-all-3.2.2/.github/instructions/coding-standards/csharp/csharp-tests.instructions.md` - xUnit organization and naming conventions
* `c:/Users/bkierun/.vscode/extensions/ise-hve-essentials.hve-core-all-3.2.2/.github/instructions/hve-core/markdown.instructions.md` - Markdown conventions
* `c:/Users/bkierun/.vscode/extensions/ise-hve-essentials.hve-core-all-3.2.2/.github/instructions/hve-core/writing-style.instructions.md` - Technical writing conventions

## Implementation Checklist

### [x] Implementation Phase 1: Supplier Contracts and Mapping

<!-- parallelizable: false -->

* [x] Step 1.1: Create Supplier response, create, and replacement request records
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 12-33)
* [x] Step 1.2: Create the Supplier AutoMapper profile
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 34-52)
* [x] Step 1.3: Validate contract and mapping compilation
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 53-59)

### [x] Implementation Phase 2: Supplier CQRS Operations

<!-- parallelizable: true -->

* [x] Step 2.1: Implement list and by-ID Supplier queries
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 64-86)
* [x] Step 2.2: Implement create, replacement, and guarded delete commands
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 87-111)
* [x] Step 2.3: Validate the combined CQRS layer
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 112-118)

Query and command files can be implemented in parallel after Phase 1. Their shared API build runs after both work items complete.

### [x] Implementation Phase 3: Supplier HTTP Surface

<!-- parallelizable: true -->

* [x] Step 3.1: Implement collection and item read endpoints
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 123-141)
* [x] Step 3.2: Implement create, replacement, and delete endpoints
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 142-164)
* [x] Step 3.3: Add executable Supplier HTTP examples
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 165-186)
* [x] Step 3.4: Validate endpoint compilation
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 187-193)

Read and mutation endpoint classes can be implemented in parallel after Phase 2. HTTP examples follow once both endpoint groups establish their final contracts.

### [x] Implementation Phase 4: Automated Tests and Repository Guidance

<!-- parallelizable: false -->

* [x] Step 4.1: Add the direct API project reference to the test project
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 198-218)
* [x] Step 4.2: Add database-free Supplier validator tests
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 219-237)
* [x] Step 4.3: Add Supplier profile and scalar mapping tests
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 238-256)
* [x] Step 4.4: Add Aspire-based Supplier OpenAPI contract tests
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 257-280)
* [x] Step 4.5: Correct repository test guidance
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 281-299)
* [x] Step 4.6: Build the solution and run the complete test project
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 300-307)

The test project reference is a shared prerequisite. Test files may be authored independently afterward, but this phase remains sequential to avoid concurrent project and shared AppHost test execution changes.

### [ ] Implementation Phase 5: Final Validation

<!-- parallelizable: false -->

* [x] Step 5.1: Run the full solution build and complete test suite
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 312-322)
* [ ] Step 5.2: Verify SQL identity, foreign-key, and CRUD assumptions when an isolated database is available
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 323-333)
* [x] Step 5.3: Fix minor validation issues and rerun focused then full checks
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 334-337)
* [x] Step 5.4: Report runtime blockers that require additional research or infrastructure
  * Details: `.copilot-tracking/details/2026-09-06/suppliers-crud-web-api-details.md` (Lines 338-341)

## Planning Log

See `.copilot-tracking/plans/logs/2026-09-06/suppliers-crud-web-api-log.md` for discrepancy tracking, implementation paths considered, and suggested follow-on work.

## Dependencies

* .NET 10 SDK and existing NuGet package graph
* Existing Product CRUD implementation as the local behavioral precedent
* Existing Northwind EF Core model and `NorthwindDB` connection contract
* Isolated Northwind-compatible SQL Server only for manual mutation verification
* Reprise endpoint discovery and current Aspire AppHost test harness

## Success Criteria

* Supplier collection and item paths expose five operations with the researched status semantics - Traces to: user CRUD requirement and primary research route contract
* Queries use no-tracking projection, deterministic ordering, async materialization, and cancellation - Traces to: framework guidance and Product query precedent
* Mutations validate schema limits, preserve full replacement semantics, and reject deletion while Products reference a Supplier - Traces to: user convention requirement and Supplier schema research
* Generated EF Core files and global middleware registration remain unchanged - Traces to: generated-code exclusion and assembly discovery research
* Validator, mapping, and OpenAPI tests pass without requiring Northwind connectivity - Traces to: focused validation requirement and plan-readiness research
* Full solution build and complete test project pass, with the known `ASPIRE010` warning documented rather than treated as a Supplier regression - Traces to: repository validation guidance
* SQL-backed CRUD behavior is verified only against an isolated database or reported as blocked - Traces to: unresolved runtime facts in the primary research and DD-02