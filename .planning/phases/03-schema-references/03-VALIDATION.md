---
phase: 3
slug: schema-references
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj, tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Category=SchemaRef" --no-build -q` |
| **Full suite command** | `dotnet test --no-build -q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --no-build -q`
- **After every plan wave:** Run `dotnet test --no-build -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Ref_To_Defs" -x` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Direct_Cycle" -x` | ❌ W0 | ⬜ pending |
| 03-01-03 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Mutual_Cycle" -x` | ❌ W0 | ⬜ pending |
| 03-01-04 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Transitive_Cycle" -x` | ❌ W0 | ⬜ pending |
| 03-01-05 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Unresolvable_Ref" -x` | ❌ W0 | ⬜ pending |
| 03-01-06 | 01 | 1 | SCHM-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Multiple_Refs_Same_Target" -x` | ❌ W0 | ⬜ pending |
| 03-02-01 | 02 | 1 | SCHM-04 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Anchor_Resolution" -x` | ❌ W0 | ⬜ pending |
| 03-02-02 | 02 | 1 | SCHM-04 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Duplicate_Anchor" -x` | ❌ W0 | ⬜ pending |
| 03-03-01 | 03 | 1 | SCHM-06 | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~SchemaRegistryTests" -x` | ❌ W0 | ⬜ pending |
| 03-03-02 | 03 | 1 | SCHM-06 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Cross_Schema_Ref" -x` | ❌ W0 | ⬜ pending |
| 03-03-03 | 03 | 1 | SCHM-06 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Cross_Schema_Unregistered" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/SchemaRefResolutionTests.cs` — stubs for SCHM-03, SCHM-04, SCHM-06 (cross-schema via registry)
- [ ] `tests/Gluey.Contract.Tests/SchemaRegistryTests.cs` — stubs for SCHM-06 (registry API in core package)

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
