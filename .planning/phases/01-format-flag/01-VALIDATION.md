---
phase: 1
slug: format-flag
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-03-19
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | `tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Tests --no-restore -f net10.0` |
| **Full suite command** | `dotnet test --no-restore` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Tests --no-restore -f net10.0`
- **After every plan wave:** Run `dotnet test --no-restore`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-01 | 01 | 1 | CORE-01 | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~ParsedPropertyFormatTests" --no-restore -f net10.0` | ❌ W0 | ⬜ pending |
| 01-01-02 | 01 | 1 | CORE-01, CORE-02 | unit+regression | `dotnet test --no-restore` | ✅ (existing) + ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs` — stubs for CORE-01 (binary format dispatch in GetXxx methods)
- No framework install needed — NUnit 4.3.1 + FluentAssertions 8.0.1 already in place
- No shared fixtures needed — tests use direct struct construction

*Task 1 in the plan IS Wave 0 — it writes the test file as the RED phase of TDD.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-19
