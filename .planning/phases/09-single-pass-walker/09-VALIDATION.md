---
phase: 9
slug: single-pass-walker
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-10
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalker" --no-build -q` |
| **Full suite command** | `dotnet test --no-build -q` |
| **Estimated runtime** | ~10 seconds |

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
| 09-01-01 | 01 | 1 | INTG-01 | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalkerTests" -q` | ❌ W0 | ⬜ pending |
| 09-01-02 | 01 | 1 | INTG-01 | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalkerTests" -q` | ❌ W0 | ⬜ pending |
| 09-01-03 | 01 | 1 | INTG-01 | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalkerTests" -q` | ❌ W0 | ⬜ pending |
| 09-02-01 | 02 | 1 | INTG-02 | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NestedPropertyAccessTests" -q` | ❌ W0 | ⬜ pending |
| 09-02-02 | 02 | 1 | INTG-03 | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ArrayElementAccessTests" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/SchemaWalkerTests.cs` — stubs for INTG-01 (core walker + validation + offset table)
- [ ] `tests/Gluey.Contract.Json.Tests/NestedPropertyAccessTests.cs` — stubs for INTG-02 (hierarchical indexers)
- [ ] `tests/Gluey.Contract.Json.Tests/ArrayElementAccessTests.cs` — stubs for INTG-03 (array buffer + element indexers)

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
