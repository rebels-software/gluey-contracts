---
phase: 1
slug: core-types
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-08
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Tests --filter "Category!=Slow" --no-build -q` |
| **Full suite command** | `dotnet test tests/Gluey.Contract.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Tests --no-build -q`
- **After every plan wave:** Run `dotnet test` (full solution)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-01 | 01 | 1 | CORE-01 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParsedPropertyTests -q` | ❌ W0 | ⬜ pending |
| 01-01-02 | 01 | 1 | CORE-02 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParsedPropertyTests -q` | ❌ W0 | ⬜ pending |
| 01-01-03 | 01 | 1 | CORE-03 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~OffsetTableTests -q` | ❌ W0 | ⬜ pending |
| 01-01-04 | 01 | 1 | CORE-04 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ValidationErrorTests -q` | ❌ W0 | ⬜ pending |
| 01-01-05 | 01 | 1 | CORE-05 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ErrorCollectorTests -q` | ❌ W0 | ⬜ pending |
| 01-01-06 | 01 | 1 | CORE-06 | unit | `dotnet test tests/Gluey.Contract.Tests --filter FullyQualifiedName~ParseResultTests -q` | ❌ W0 | ⬜ pending |
| 01-01-07 | 01 | 1 | CORE-07 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter FullyQualifiedName~JsonContractSchemaTests -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Tests/ParsedPropertyTests.cs` — stubs for CORE-01, CORE-02
- [ ] `tests/Gluey.Contract.Tests/OffsetTableTests.cs` — stubs for CORE-03
- [ ] `tests/Gluey.Contract.Tests/ValidationErrorTests.cs` — stubs for CORE-04
- [ ] `tests/Gluey.Contract.Tests/ErrorCollectorTests.cs` — stubs for CORE-05
- [ ] `tests/Gluey.Contract.Tests/ParseResultTests.cs` — stubs for CORE-06
- [ ] `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` — stubs for CORE-07

*If none: "Existing infrastructure covers all phase requirements."*

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
