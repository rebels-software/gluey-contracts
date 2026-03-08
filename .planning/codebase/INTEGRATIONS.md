# External Integrations

## Current State

**No external integrations exist yet.** The codebase is in early development with only stub implementations.

## CI/CD

- GitHub Actions workflow referenced in README badge: `main.yml`
- Codecov integration referenced in README badge
- Repository: `rebels-software/gluey-contract` on GitHub

## NuGet

- Libraries configured for NuGet packaging (`PackageReadmeFile`, `PackageIcon`)
- Package ID: `Gluey.Contract`, `Gluey.Contract.Json`

## Ecosystem Context

Part of the **Gluey ecosystem** (see `README.md`):
- Gluey DSL generates schemas consumed by Gluey.Contract
- Standalone usage supported with standard JSON Schema files
- Designed for use with HTTP request validation, database protocols, message brokers

## Planned Integrations (from ADRs and README)

| Integration | Package | Status |
|-------------|---------|--------|
| JSON Schema validation | `Gluey.Contract.Json` | Stub exists |
| ASP.NET Core model binding | `Gluey.Contract.AspNetCore` | Planned |
| Protobuf wire format | `Gluey.Contract.Protobuf` | Planned |
| PostgreSQL wire protocol | `Gluey.Contract.Postgres` | Planned |
| Redis RESP protocol | `Gluey.Contract.Redis` | Planned |
