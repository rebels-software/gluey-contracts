---
phase: 04
slug: leaf-types
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-20
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q` |
| **Full suite command** | `dotnet test --no-restore -v q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q`
- **After every plan wave:** Run `dotnet test --no-restore -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-test-01 | tests | 2 | STRE-01 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~StringParsing" --no-restore -v q` | ❌ | ⬜ pending |
| 04-test-02 | tests | 2 | STRE-02 | integration | same filter | ❌ | ⬜ pending |
| 04-test-03 | tests | 2 | STRE-03 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~EnumParsing" --no-restore -v q` | ❌ | ⬜ pending |
| 04-test-04 | tests | 2 | STRE-04 | integration | same filter | ❌ | ⬜ pending |
| 04-test-05 | tests | 2 | BITS-01 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~BitFieldParsing" --no-restore -v q` | ❌ | ⬜ pending |
| 04-test-06 | tests | 2 | BITS-02 | integration | same filter | ❌ | ⬜ pending |
| 04-test-07 | tests | 2 | BITS-03 | integration | same filter | ❌ | ⬜ pending |
| 04-test-08 | tests | 2 | BITS-04 | integration | same filter | ❌ | ⬜ pending |
| 04-test-09 | tests | 2 | COMP-04 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~PaddingParsing" --no-restore -v q` | ❌ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No Wave 0 plan — implementation-first ordering chosen deliberately. Tests created in a later wave after implementation. Framework already configured (NUnit + FluentAssertions).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| ADR-16 doc accuracy | D-03, D-06 | Documentation review | Verify string mode field and enum convention are correctly documented in ADR |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
