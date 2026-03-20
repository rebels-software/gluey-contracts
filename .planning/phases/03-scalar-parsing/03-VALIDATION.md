---
phase: 03
slug: scalar-parsing
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-20
---

# Phase 03 — Validation Strategy

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
| 03-01-01 | 01 | 1 | SCLR-01 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~ScalarParsing" --no-restore -v q` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | SCLR-02 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-03 | 01 | 1 | SCLR-03 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-04 | 01 | 1 | SCLR-04 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-05 | 01 | 1 | SCLR-05 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-06 | 01 | 1 | SCLR-06 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-07 | 01 | 1 | CORE-04 | unit | same filter | ❌ W0 | ⬜ pending |
| 03-01-08 | 01 | 1 | CORE-05 | unit | same filter | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` — stubs for SCLR-01 through SCLR-06, CORE-04
- [ ] Test contract JSON files (pure-scalar contracts for big-endian, little-endian, mixed, truncated cases)
- No framework install needed — NUnit + FluentAssertions already configured

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Zero-alloc hot path | CORE-05 | Allocation profiling requires BenchmarkDotNet or allocation counter | Run `dotnet test` with `[MemoryDiagnoser]` attribute or verify ArrayPool rent/return coverage in code review |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
