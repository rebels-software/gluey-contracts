---
phase: 6
slug: constraint-validation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | VALD-06 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" --no-build` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | VALD-06 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NumericValidatorTests" --no-build` | ❌ W0 | ⬜ pending |
| 06-01-03 | 01 | 1 | VALD-07 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~StringValidatorTests" --no-build` | ❌ W0 | ⬜ pending |
| 06-01-04 | 01 | 1 | VALD-07 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~JsonSchemaLoadingTests" --no-build` | ✅ | ⬜ pending |
| 06-02-01 | 02 | 1 | VALD-08 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ArrayValidatorTests" --no-build` | ❌ W0 | ⬜ pending |
| 06-02-02 | 02 | 1 | VALD-08 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ObjectValidatorTests" --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/NumericValidatorTests.cs` — stubs for VALD-06
- [ ] `tests/Gluey.Contract.Json.Tests/StringValidatorTests.cs` — stubs for VALD-07
- [ ] `tests/Gluey.Contract.Json.Tests/ArrayValidatorTests.cs` — stubs for VALD-08 (array)
- [ ] `tests/Gluey.Contract.Json.Tests/ObjectValidatorTests.cs` — stubs for VALD-08 (object)

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
