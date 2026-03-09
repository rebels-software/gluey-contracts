---
phase: 5
slug: basic-validation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-09
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~KeywordValidator" --no-build -q` |
| **Full suite command** | `dotnet test --no-build -q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~KeywordValidator" --no-build -q`
- **After every plan wave:** Run `dotnet test --no-build -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | VALD-01 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateType" -q` | ❌ W0 | ⬜ pending |
| 05-01-02 | 01 | 1 | VALD-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateEnum" -q` | ❌ W0 | ⬜ pending |
| 05-01-03 | 01 | 1 | VALD-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateConst" -q` | ❌ W0 | ⬜ pending |
| 05-01-04 | 01 | 1 | VALD-03 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ValidateRequired" -q` | ❌ W0 | ⬜ pending |
| 05-01-05 | 01 | 1 | VALD-04 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~AdditionalProperty" -q` | ❌ W0 | ⬜ pending |
| 05-02-01 | 02 | 1 | VALD-05 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~ItemSchema" -q` | ❌ W0 | ⬜ pending |
| 05-02-02 | 02 | 1 | VALD-17 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Keyword" -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Json.Tests/KeywordValidatorTests.cs` — stubs for VALD-01 through VALD-05, VALD-17
- No framework install needed — NUnit 4.3.1 + FluentAssertions 8.0.1 already configured
- No new conftest/fixtures needed — tests construct SchemaNode and ErrorCollector directly

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
