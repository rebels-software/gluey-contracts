---
phase: 2
slug: schema-model
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaLoading" --no-build -q` |
| **Full suite command** | `dotnet test --no-build -q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --no-build -q`
- **After every plan wave:** Run `dotnet test --no-build -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | SCHM-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_FromBytes" -q` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | SCHM-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_FromString" -q` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | SCHM-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Load_FromBytes" -q` | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | SCHM-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Load_FromString" -q` | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 1 | SCHM-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_InvalidJson" -q` | ❌ W0 | ⬜ pending |
| 02-02-01 | 02 | 1 | SCHM-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "SchemaNode_Immutable" -q` | ❌ W0 | ⬜ pending |
| 02-02-02 | 02 | 1 | SCHM-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "RootNode_Path" -q` | ❌ W0 | ⬜ pending |
| 02-02-03 | 02 | 1 | SCHM-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "NestedProperties_Paths" -q` | ❌ W0 | ⬜ pending |
| 02-02-04 | 02 | 1 | SCHM-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PathEscaping" -q` | ❌ W0 | ⬜ pending |
| 02-03-01 | 03 | 1 | SCHM-05 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PropertyOrdinals" -q` | ❌ W0 | ⬜ pending |
| 02-03-02 | 03 | 1 | SCHM-05 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PropertyCount" -q` | ❌ W0 | ⬜ pending |
| 02-03-03 | 03 | 1 | SCHM-05 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "DepthFirst_Ordinals" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs` — stubs for SCHM-01
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs` — stubs for SCHM-02, SCHM-05 (via InternalsVisibleTo)

*Existing infrastructure covers test framework setup.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
