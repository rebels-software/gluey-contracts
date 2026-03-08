# Technology Stack

## Language & Runtime

| Property | Value |
|----------|-------|
| Language | C# 13 |
| Runtime | .NET 9.0 |
| SDK | Microsoft.NET.Sdk |
| Nullable | enabled |
| ImplicitUsings | enabled |

## Projects

| Project | Type | Version |
|---------|------|---------|
| `Gluey.Contract` | Class library (core) | 0.1.0 |
| `Gluey.Contract.Json` | Class library (format driver) | 0.1.0 |
| `Gluey.Contract.Tests` | Test project | - |
| `Gluey.Contract.Json.Tests` | Test project | - |

## Dependencies

### Core (`src/Gluey.Contract/`)
- **Zero NuGet dependencies** — by design (ADR 7)
- Only depends on .NET base framework

### JSON Driver (`src/Gluey.Contract.Json/`)
- References `Gluey.Contract` (project reference)
- No additional NuGet dependencies (may use `System.Text.Json` from framework)

### Test Projects
| Package | Version |
|---------|---------|
| NUnit | 4.3.1 |
| NUnit3TestAdapter | 4.6.0 |
| NUnit.Analyzers | 4.5.0 |
| FluentAssertions | 8.0.1 |
| Microsoft.NET.Test.Sdk | 17.12.0 |
| coverlet.collector | 6.0.2 |

## Build Configuration

- Solution file: `Gluey.Contract.sln`
- Configurations: Debug, Release (Any CPU)
- NuGet packaging enabled on libraries (PackageIcon, PackageReadmeFile set)
- `InternalsVisibleTo` from `Gluey.Contract` to `Gluey.Contract.Tests` and `Gluey.Contract.Json`
- `InternalsVisibleTo` from `Gluey.Contract.Json` to `Gluey.Contract.Json.Tests`

## Planned Packages (from README)

| Package | Description |
|---------|-------------|
| `Gluey.Contract.AspNetCore` | ASP.NET Core integration |
| `Gluey.Contract.Protobuf` | Protobuf wire format parser |
| `Gluey.Contract.Postgres` | PostgreSQL wire protocol reader |
| `Gluey.Contract.Redis` | Redis RESP protocol reader |
