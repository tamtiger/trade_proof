# ADR 0001: Runtime and Frontend

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

`TP-PLAN` requires a web client, application API, background workers, deterministic finance, strong tenant boundaries, durable jobs and CI/test skeleton. The local environment has .NET SDK 10.0.301 and ASP.NET Core 10 runtime installed.

## Decision

Use .NET 10 and ASP.NET Core Minimal APIs for the application API and worker host. Start with a server-rendered/static responsive web shell served by ASP.NET Core; introduce a TypeScript client build only when UI complexity requires it. Financial/accounting/context computations remain server-side and never run as client source of truth.

## Alternatives

- Node/TypeScript full stack: available locally, but would increase runtime surface before the backend contracts exist.
- Go: rejected because Go is not installed in the current environment.
- Native mobile first: outside MVP.

## Security/privacy impact

ASP.NET Core keeps auth, authorization, upload, export and deletion policy on the server boundary. Client code may validate for usability but cannot authorize or calculate finance.

## Rollback

If .NET becomes unsuitable before Phase 1, replace this ADR and regenerate the skeleton before adding product code. After Phase 1 persistence code exists, migration requires an explicit replatform plan.

