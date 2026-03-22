---
phase: 06
slug: validation
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-22
---

# Phase 06 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests" --no-build` |
| **Full suite command** | `dotnet test tests/Gluey.Contract.Binary.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests" --no-build`
- **After every plan wave:** Run `dotnet test tests/Gluey.Contract.Binary.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-T1 | 01 | 1 | VALD-01, VALD-02, VALD-03 | build | `dotnet build src/Gluey.Contract.Binary --no-restore -q` | ❌ W0 | ⬜ pending |
| 06-01-T2 | 01 | 1 | VALD-01, VALD-02, VALD-03, VALD-05 | build+test | `dotnet build && dotnet test tests/Gluey.Contract.Binary.Tests --no-build -q` | ✅ | ⬜ pending |
| 06-02-T1 | 02 | 2 | VALD-01, VALD-02, VALD-03, VALD-04, VALD-05 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "ClassName~ValidationTests" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements. No Wave 0 stubs needed — Plan 01 creates implementation, Plan 02 creates and runs tests.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 5s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-22
