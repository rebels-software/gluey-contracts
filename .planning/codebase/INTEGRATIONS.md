# External Integrations

**Analysis Date:** 2026-03-18

## APIs & External Services

**None - This is a library, not a service consumer.**

Gluey.Contract is a standalone .NET library for schema-driven validation. It does not depend on or integrate with external APIs or services.

## Data Storage

**Databases:** Not applicable

**File Storage:** Not applicable - operates entirely in-memory on provided byte buffers

**Caching:** Not applicable

## Authentication & Identity

**Auth Provider:** Not applicable

This library does not authenticate with external services.

## Monitoring & Observability

**Error Tracking:** Not integrated

Code coverage is reported to **codecov.io** via GitHub Actions:
- CI environment variable: `CODE_COV_TOKEN`
- Used by workflow: `.github/workflows/main.yml`
- Triggered on: Build success, reported via coverlet.collector

**Logs:** Not applicable

The library uses no logging framework. Error information is returned via `ValidationError` objects with error codes and messages.

## CI/CD & Deployment

**Hosting:** NuGet.org package registry

**CI Pipeline:** GitHub Actions

Workflow file: `.github/workflows/main.yml`

**Build & Test:**
- Triggered on: Push to main, tags matching `contract/v*` or `contract-json/v*`, pull requests to main
- Uses shared workflows from: `rebels-software/github-actions@v1.1.0`
  - `build-and-test.yaml` - Builds and tests against .NET 9.0.x and 10.0.x
  - `create-version-tag.yaml` - Auto-generates version tags from project files
  - `publish-nuget.yaml` - Publishes packages to NuGet.org

**Publishing:**
- Packages: `Gluey.Contract` and `Gluey.Contract.Json`
- Trigger: Push to version tags or after successful build on main branch
- Authentication: `NUGET_API_KEY` secret
- Target: NuGet.org (public package feed)

## Environment Configuration

**Required env vars:**
- `CI` - Set to `true` to enable continuous integration build mode (applies source embedding and deterministic builds)
- `NUGET_API_KEY` - GitHub Actions secret for NuGet.org publishing
- `CODE_COV_TOKEN` - GitHub Actions secret for codecov.io integration

**Secrets location:**
- GitHub Actions secrets (repository settings)
- Never committed to version control

## Webhooks & Callbacks

**Incoming:** Not applicable

**Outgoing:**
- GitHub Actions triggers NuGet.org publish API when packages are ready
- Codecov.io webhook integration for coverage reporting

---

*Integration audit: 2026-03-18*
