---
phase: 7
slug: composition-and-conditionals
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator|ClassName~ConditionalValidator|ClassName~DependencyValidator" --no-build -q` |
| **Full suite command** | `dotnet test tests/Gluey.Contract.Json.Tests -q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator|ClassName~ConditionalValidator|ClassName~DependencyValidator" --no-build -q`
- **After every plan wave:** Run `dotnet test tests/Gluey.Contract.Json.Tests -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | VALD-09 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~CompositionValidator" -q` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | VALD-10 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ConditionalValidator" -q` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 1 | VALD-11 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~DependencyValidator" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/CompositionValidatorTests.cs` — stubs for VALD-09
- [ ] `tests/Gluey.Contract.Json.Tests/ConditionalValidatorTests.cs` — stubs for VALD-10
- [ ] `tests/Gluey.Contract.Json.Tests/DependencyValidatorTests.cs` — stubs for VALD-11

*Existing infrastructure covers framework install — NUnit and FluentAssertions already configured.*

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
