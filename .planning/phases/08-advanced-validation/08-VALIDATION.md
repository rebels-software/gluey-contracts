---
phase: 8
slug: advanced-validation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-10
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty|ClassName~Contains|ClassName~UniqueItems|ClassName~Format|ClassName~SchemaOptions" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --no-build -x`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | VALD-12 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty" -x` | ❌ W0 | ⬜ pending |
| 08-01-02 | 01 | 1 | VALD-12 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~PatternProperty" -x` | ❌ W0 | ⬜ pending |
| 08-01-03 | 01 | 1 | VALD-13 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Contains" -x` | ❌ W0 | ⬜ pending |
| 08-01-04 | 01 | 1 | VALD-14 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~UniqueItems" -x` | ❌ W0 | ⬜ pending |
| 08-02-01 | 02 | 1 | VALD-15 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaOptions" -x` | ❌ W0 | ⬜ pending |
| 08-02-02 | 02 | 1 | VALD-15, VALD-16 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~Format" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/PatternPropertyValidatorTests.cs` — stubs for VALD-12
- [ ] `tests/Gluey.Contract.Json.Tests/ContainsValidatorTests.cs` — stubs for VALD-13
- [ ] `tests/Gluey.Contract.Json.Tests/UniqueItemsValidatorTests.cs` — stubs for VALD-14
- [ ] `tests/Gluey.Contract.Json.Tests/FormatValidatorTests.cs` — stubs for VALD-15, VALD-16
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaOptionsTests.cs` — stubs for VALD-15

*Existing infrastructure covers framework install — NUnit 4.3.1 and FluentAssertions 8.0.1 already configured.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
