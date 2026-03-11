# Invariants

System invariants that must hold true at all times. These are non-negotiable rules enforced by design, tests, and code review.

Each invariant is documented in its own file with rationale and verification strategy.

| # | Invariant | File |
|---|-----------|------|
| 1 | [Zero allocations in parse path](1-zero-allocations-in-parse-path.md) | `1-zero-allocations-in-parse-path.md` |
| 2 | [Buffer ownership by caller](2-buffer-ownership-by-caller.md) | `2-buffer-ownership-by-caller.md` |
| 3 | [Paths precomputed from schema](3-paths-precomputed-from-schema.md) | `3-paths-precomputed-from-schema.md` |
| 4 | [No external dependencies in core](4-no-external-dependencies-in-core.md) | `4-no-external-dependencies-in-core.md` |
| 5 | [All errors collected per parse](5-all-errors-collected-per-parse.md) | `5-all-errors-collected-per-parse.md` |
| 6 | [Result never throws](6-result-never-throws.md) | `6-result-never-throws.md` |
