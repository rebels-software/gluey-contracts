---
phase: 10
slug: quality-and-packaging
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-10
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Allocation" --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Allocation" --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 1 | QUAL-01 | manual (benchmark run) | `dotnet run --project benchmarks/Gluey.Contract.Benchmarks -c Release` | ❌ W0 | ⬜ pending |
| 10-02-01 | 02 | 1 | QUAL-02 | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~Allocation" --no-build` | ❌ W0 | ⬜ pending |
| 10-03-01 | 03 | 1 | QUAL-03 | smoke | `dotnet pack src/Gluey.Contract/Gluey.Contract.csproj -c Release && dotnet pack src/Gluey.Contract.Json/Gluey.Contract.Json.csproj -c Release` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `benchmarks/Gluey.Contract.Benchmarks/` — entire benchmark project (csproj, Program.cs, scenario stubs)
- [ ] `tests/Gluey.Contract.Json.Tests/AllocationTests/` — allocation regression test files
- [ ] NuGet metadata additions to both csproj files

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| BenchmarkDotNet reports zero heap allocations | QUAL-01 | Benchmarks are local-only, not CI; require Release build + isolated execution | Run `dotnet run --project benchmarks/Gluey.Contract.Benchmarks -c Release`, verify `Allocated` column shows `0 B` for all non-array scenarios |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
