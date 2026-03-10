---
status: diagnosed
phase: 09-single-pass-walker
source: 09-01-SUMMARY.md, 09-02-SUMMARY.md
started: 2026-03-10T17:00:00Z
updated: 2026-03-10T17:25:00Z
---

## Current Test

[testing complete]

## Tests

### 1. TryParse validates and returns true for valid JSON
expected: Run `dotnet test` from repo root. All tests pass (398+), confirming TryParse/Parse are wired to SchemaWalker and return correct results for valid input.
result: pass

### 2. Validation errors for schema violations
expected: SchemaWalkerTests cover all 17 validator categories (type, enum, const, required, additionalProperties, numeric, string, array, object, composition, conditional, dependencies, uniqueItems, patternProperties, format). Each failing case produces appropriate ValidationErrorCode entries.
result: pass

### 3. Parse returns null for malformed JSON
expected: Passing structurally invalid JSON (e.g., `{broken`) to Parse returns null. The InvalidJson error code is used for structural JSON errors.
result: pass

### 4. Nested property access via string indexer
expected: After parsing JSON like `{"address":{"street":"Main St"}}`, accessing `result["address"]["street"]` returns a ParsedProperty with the correct value. NestedPropertyAccessTests confirm 3-level deep nesting works.
result: issue
reported: "Top-level indexer requires slash prefix result[\"/address\"] instead of result[\"address\"] — should work without the leading slash"
severity: major

### 5. Array element access via int indexer
expected: After parsing JSON like `{"tags":["a","b"]}`, accessing `result["tags"][0]` returns the first element. Out-of-bounds index throws. ArrayElementAccessTests confirm this including array-of-objects patterns like `result["items"][0]["name"]`.
result: issue
reported: "No iteration/enumeration tests for array elements — missing foreach/count support for iterating over array contents"
severity: major

### 6. ParseResult disposal cleans up ArrayBuffer
expected: ParseResult.Dispose cascades to ArrayBuffer, returning pooled arrays. No memory leaks from repeated parse/dispose cycles. Verified by code inspection or test coverage.
result: issue
reported: "ParseResult doesn't have bool disposing pattern — no guard against double-dispose, no standard Dispose(bool disposing) implementation"
severity: major

## Summary

total: 6
passed: 3
issues: 3
pending: 0
skipped: 0

## Gaps

- truth: "Property access works without leading slash: result[\"address\"][\"street\"]"
  status: failed
  reason: "User reported: Top-level indexer requires slash prefix result[\"/address\"] instead of result[\"address\"] — should work without the leading slash"
  severity: major
  test: 4
  root_cause: "SchemaIndexer.AssignOrdinals keys nameToOrdinal by full RFC 6901 paths (via SchemaNode.BuildChildPath which always prepends '/'), and ParseResult/ParsedProperty string indexers do raw TryGetValue with no normalization"
  artifacts:
    - path: "src/Gluey.Contract/ParseResult.cs"
      issue: "String indexer (line 92) does raw lookup — no slash normalization"
    - path: "src/Gluey.Contract/ParsedProperty.cs"
      issue: "String indexer (line 114) does raw lookup into _childOrdinals"
    - path: "src/Gluey.Contract.Json/SchemaIndexer.cs"
      issue: "Line 37 stores keys as RFC 6901 paths with leading slash"
  missing:
    - "Normalize at lookup time: if TryGetValue fails and name doesn't start with '/', retry with '/' + name in both ParseResult and ParsedProperty indexers"
  debug_session: ".planning/debug/slash-prefix-indexer.md"

- truth: "Array elements support iteration/enumeration (foreach, count)"
  status: failed
  reason: "User reported: No iteration/enumeration tests for array elements — missing foreach/count support for iterating over array contents"
  severity: major
  test: 5
  root_cause: "ArrayBuffer already has GetCount(arrayOrdinal) and Get(arrayOrdinal, index) but ParsedProperty exposes no Count property and no GetEnumerator() method — missing public API surface, not missing data"
  artifacts:
    - path: "src/Gluey.Contract/ParsedProperty.cs"
      issue: "Has _arrayBuffer and _arrayOrdinal fields but no Count property or GetEnumerator()"
    - path: "src/Gluey.Contract/ArrayBuffer.cs"
      issue: "GetCount() exists at line 87 but is never exposed through ParsedProperty"
  missing:
    - "Add Count property to ParsedProperty delegating to ArrayBuffer.GetCount(_arrayOrdinal)"
    - "Add struct ArrayEnumerator and GetEnumerator() to ParsedProperty following ParseResult.Enumerator zero-allocation pattern"
  debug_session: ""

- truth: "ParseResult follows standard Dispose(bool disposing) pattern"
  status: failed
  reason: "User reported: ParseResult doesn't have bool disposing pattern — no guard against double-dispose, no standard Dispose(bool disposing) implementation"
  severity: major
  test: 6
  root_cause: "OffsetTable and ErrorCollector are readonly structs whose Dispose() null-checks cannot null the field after returning (readonly struct forbids mutation), so calling Dispose twice returns the same pooled array to ArrayPool twice, corrupting the pool. ParseResult delegates blindly with no guard."
  artifacts:
    - path: "src/Gluey.Contract/ParseResult.cs"
      issue: "Dispose() calls child Dispose methods with no double-dispose guard"
    - path: "src/Gluey.Contract/OffsetTable.cs"
      issue: "readonly struct cannot null _entries after ArrayPool.Return — double-dispose returns same array twice"
    - path: "src/Gluey.Contract/ErrorCollector.cs"
      issue: "Same readonly struct problem — cannot null _errors after return"
  missing:
    - "Document single-dispose contract on ParseResult, or add indirection box (bool[] disposed holder) to OffsetTable/ErrorCollector to guard against double-dispose"
  debug_session: ""
