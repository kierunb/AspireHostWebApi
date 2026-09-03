---
title: Agent Instructions
description: Project-specific guidance for coding agents working on the WebApiMediatorCQRS solution
---

## Project Overview

This is a .NET 10 sample that combines ASP.NET Core, .NET Aspire, MediatR CQRS,
Reprise endpoints, MVC controllers, AutoMapper, FluentValidation, and EF Core.
Read [readme.md](readme.md) for the feature summary.

The solution contains these runtime projects:

* `WebApiMediatorCQRS/` is the API and owns HTTP routes, CQRS requests and handlers,
  mapping profiles, pipeline behaviors, and the Northwind data model
* `AspireAppHost/AspireAppHost.AppHost/` is the Aspire orchestration entry point
* `AspireAppHost/AspireAppHost.ServiceDefaults/` configures shared telemetry,
  health checks, service discovery, and HTTP resilience

Do not edit generated output under `bin/` or `obj/`.

## Build And Run

Run commands from the repository root:

```powershell
dotnet restore WebApiMediatorCQRS.sln
dotnet build WebApiMediatorCQRS.sln
dotnet run --project AspireAppHost/AspireAppHost.AppHost/AspireAppHost.AppHost.csproj
```

To run only the API, use:

```powershell
dotnet run --project WebApiMediatorCQRS/WebApiMediatorCQRS.csproj
```

There is currently no automated test project. After a change, build the full solution
and exercise the affected route. The API launch profiles use HTTP port `5039` and
HTTPS port `7181`; Swagger is available only in the Development environment.

The current AppHost build emits `ASPIRE010` because `AspireUseCliBundle` resolves to
`false`. The build still succeeds, but Aspire features that require the CLI bundle may
need that property enabled.

Database-backed routes require the `NorthwindDB` connection configured in
`WebApiMediatorCQRS/appsettings.json`. The default targets the `Northwind` database
on SQL Server LocalDB instance `(localdb)\sql2025`.

## Implementation Patterns

Keep changes within the existing feature boundaries and mirror the nearest example:

* Reprise HTTP endpoints use `[Endpoint]` and `[Get]` or `[Post]`, then become active
  through `app.MapEndpoints()`. See
  [PingEndpoint.cs](WebApiMediatorCQRS/Endpoints/PingEndpoint.cs) and
  [GetCustomerByIdEndpoint.cs](WebApiMediatorCQRS/Endpoints/GetCustomerByIdEndpoint.cs)
* MVC endpoints remain in `Controllers/`. See
  [PingController.cs](WebApiMediatorCQRS/Controllers/PingController.cs)
* Commands, queries, validators, response types, and handlers are colocated in the
  corresponding `Commands/` or `Queries/` feature file. See
  [PingCommand.cs](WebApiMediatorCQRS/Commands/PingCommand.cs) and
  [GetCustomerByIdQuery.cs](WebApiMediatorCQRS/Queries/GetCustomerByIdQuery.cs)
* Add request and response mappings to a profile under `Profiles/`. For EF queries,
  preserve `ProjectTo` on `IQueryable` so projection occurs in SQL
* Use descriptive PascalCase type and method names, file-scoped namespaces, nullable
  annotations, cancellation tokens for async operations, and structured logging

## Pipeline And Data Pitfalls

`Program.cs` is the source of truth for middleware and dependency injection order.
Only `LoggingBehavior<,>` is active in the MediatR pipeline. The validation and
caching behaviors are commented out, so do not assume cross-cutting validation or
caching is enabled.

Some routes validate explicitly with `IValidator<T>`. Before enabling
`ValidationBehavior<,>`, remove duplicate route-level validation and verify that
`GlobalExceptionHandler` still produces the expected HTTP 400 response. The disabled
`CachingBehavior<,>` only applies to requests implementing `ICacheable`.

Files in `WebApiMediatorCQRS/Database/` were reverse-engineered with EF Core Power
Tools. Treat generated entities and `NorthwindContext.cs` as generated code; prefer
configuration or partial extensions where possible, and expect regeneration to
overwrite direct edits.

## Validation Checklist

* Build `WebApiMediatorCQRS.sln` after every code change
* Run the API or Aspire AppHost and exercise each affected HTTP route
* Check database-backed changes against a reachable Northwind LocalDB instance
* Confirm both Reprise endpoint discovery and MVC routing when registration changes
* Verify validation failures and unhandled exceptions through the global problem
  details handler