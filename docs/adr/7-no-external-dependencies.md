# ADR 7: No External Dependencies in Core

## Status
Accepted

## Context
A library intended for high-performance, zero-allocation parsing should not bring transitive dependencies that may allocate, conflict with consumer versions, or increase package size.

## Decision
`Gluey.Contract` (core) has zero NuGet dependencies beyond the .NET base framework.

Format-specific packages may depend on framework libraries:
- `Gluey.Contract.Json` may use `System.Text.Json` types (part of the framework, not a NuGet dependency).
- `Gluey.Contract.AspNetCore` depends on ASP.NET Core packages.

## Consequences
- Core remains lightweight and conflict-free.
- No version conflicts with consumer applications.
- Format packages explicitly declare their dependencies.
