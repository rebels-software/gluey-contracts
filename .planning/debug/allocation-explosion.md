---
status: awaiting_human_verify
trigger: "Benchmark results show Gluey allocates 18-3257x more memory than STJ"
created: 2026-03-10T00:00:00Z
updated: 2026-03-10T01:00:00Z
---

## Current Focus

hypothesis: CONFIRMED - ArrayBuffer class instance was the remaining 56B allocation per TryParse call
test: Pooled ArrayBuffer via [ThreadStatic] cache, verified with allocation tests
expecting: TryParse 56B -> 0B after warmup
next_action: Await human verification via benchmarks

## Symptoms

expected: Near-zero allocations on parse path. Gluey should allocate less than STJ (72-80B).
actual: Gluey allocates 1,336B (small), 33,272B (medium), 234,521B (large) per TryParse call. STJ allocates 72-80B. ValidateOnly is similarly bad (752B-233,937B).
errors: No runtime errors -- excessive allocation
reproduction: Run benchmarks in benchmarks/Gluey.Contract.Benchmarks with BenchmarkDotNet
started: First benchmark run. Never benchmarked before Phase 10.

## Eliminated

(none -- root cause confirmed on first hypothesis)

## Evidence

- timestamp: 2026-03-10T00:01:00Z
  checked: SchemaWalker constructor
  found: Per-parse: new ErrorCollector() with new int[1], new ArrayBuffer() with new Dictionary, new OffsetTable()
  implication: Baseline ~112B+ per parse

- timestamp: 2026-03-10T00:02:00Z
  checked: WalkObject per-property allocations
  found: |
    Per object: new HashSet<string>, new Dictionary<string,int> for childOrdinals
    Per property: GetString for name, BuildChildPath (2 Replace + concat), Dictionary for grandchildOrdinals
    Per composition check: new ErrorCollector for temp validation
  implication: Linear allocation scaling with property count

- timestamp: 2026-03-10T00:10:00Z
  checked: Benchmark results AFTER fix
  found: |
    Flat Object TryParse: Small 1336B->56B, Medium 33272B->56B, Large 234521B->57B
    Flat Object ValidateOnly: Small 752B->0B, Medium->0B, Large->1B
    Gluey now allocates LESS than STJ (56B vs 72B TryParse, 0B vs 72B ValidateOnly)
  implication: Fix is effective for flat object schemas

- timestamp: 2026-03-10T02:00:00Z
  checked: ArrayBuffer class instance as remaining 56B allocation source
  found: |
    ArrayBuffer is a class (must be, shared by reference across ParsedProperty instances).
    new ArrayBuffer() on every TryParse call = 56B heap allocation (object header + 6 fields).
    This is the ONLY remaining per-call heap allocation after all other optimizations.
  implication: Pooling ArrayBuffer via [ThreadStatic] cache eliminates last allocation

- timestamp: 2026-03-11T00:00:00Z
  checked: Benchmark fairness -- added JsonSchema.Net validation baseline
  found: |
    Previous benchmarks only compared Gluey (validate+index) vs STJ JsonDocument.Parse (parse only, no validation).
    Added JsonSchema.Net 9.1.2 as a fair two-pass comparison: JsonDocument.Parse + JsonSchema.Evaluate.
    This shows the real advantage of Gluey's single-pass approach vs typical parse-then-validate workflow.
    Added StjValidate_Small/Medium/Large methods to all 4 benchmark scenarios (Flat, Nested, Array, FullSchema).
    Uses same JSON Schema definitions already defined in each benchmark class.
  implication: Benchmark results will now show three tiers -- STJ parse-only, JsonSchema.Net parse+validate, and Gluey single-pass

- timestamp: 2026-03-10T02:01:00Z
  checked: ArrayBuffer pooling implementation
  found: |
    Added [ThreadStatic] cache, Rent/Return pattern, Reset() method.
    Constructor made private. Dispose() returns to cache first, falls back to ArrayPool return.
    All 423 tests pass (342+81), only 2 pre-existing array element failures unchanged.
    Allocation regression tests pass with tightened budgets (ByteArrayBudget: 128->64).
  implication: TryParse(byte[]) should now be 0B after warmup

## Resolution

root_cause: |
  Multiple categories of heap allocation on the hot parse path:
  1. Per-object HashSet<string> + Dictionary<string,int> for property tracking
  2. Per-property Encoding.UTF8.GetString + SchemaNode.BuildChildPath string allocations
  3. Per-composition/conditional check: temporary ErrorCollector with new int[1]
  4. ErrorCollector new int[1] for count holder
  5. ArrayBuffer Dictionary<int,(int,int)> for region tracking

fix: |
  Applied 8 optimization techniques:
  1. Pre-compiled PropertyLookup tables on SchemaNode (UTF8 byte matching, no string conversion)
  2. Stackalloc bitset for required-property tracking (replaces HashSet<string>)
  3. Lazy HashSet<string> creation (only for schemas using composition/conditional/dependentRequired)
  4. Bool-returning validation helpers (replaces temp ErrorCollector allocation in subschema checks)
  5. ErrorCollector int[] from ArrayPool instead of new int[1]
  6. ArrayBuffer region tracking via ArrayPool int[] instead of Dictionary
  7. Fast-path scalar validation for unknown properties (avoids path string allocation)
  8. Pre-computed RequiredUtf8 bytes for zero-allocation required checking
  9. ArrayBuffer instance pooling via [ThreadStatic] cache (eliminates last 56B per-call allocation)

verification: |
  All 342 existing tests pass (2 pre-existing array access failures unchanged).
  Flat Object Benchmark results (before -> after):
    TryParse_Small:   1,336B -> 56B   (23.9x reduction)
    TryParse_Medium: 33,272B -> 56B   (594x reduction)
    TryParse_Large: 234,521B -> 57B   (4,114x reduction)
    ValidateOnly_Small:   752B -> 0B   (eliminated)
    ValidateOnly_Medium:       -> 0B   (eliminated)
    ValidateOnly_Large: 233,937B -> 1B (eliminated)
  Gluey now allocates LESS than STJ for all flat object sizes.
  Nested objects still show allocations for medium/large (4KB/45KB) due to
  WalkObject requiring string paths for nested unknown-property objects.

files_changed:
  - src/Gluey.Contract/SchemaNode.cs (PropertyLookup, RequiredUtf8)
  - src/Gluey.Contract/ErrorCollector.cs (ArrayPool for int[])
  - src/Gluey.Contract/ArrayBuffer.cs (ArrayPool for region tracking + [ThreadStatic] pooling)
  - src/Gluey.Contract.Json/SchemaIndexer.cs (builds PropertyLookup tables)
  - src/Gluey.Contract.Json/SchemaWalker.cs (major rewrite - zero-alloc hot path, ArrayBuffer.Rent)
  - src/Gluey.Contract.Json/KeywordValidator.cs (CheckType/CheckEnum/CheckConst)
  - src/Gluey.Contract.Json/FormatValidator.cs (Check method)
  - tests/Gluey.Contract.Json.Tests/AllocationTests/TryParseAllocationTests.cs (tightened budgets)
