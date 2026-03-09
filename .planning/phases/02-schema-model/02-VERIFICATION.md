---
phase: 02-schema-model
verified: 2026-03-09T12:55:00Z
status: passed
score: 15/15 must-haves verified
must_haves:
  truths:
    - "SchemaNode is an internal sealed class with all Draft 2020-12 keyword fields"
    - "SchemaNode is immutable -- all properties set in constructor, no public setters"
    - "SchemaNode carries a precomputed RFC 6901 JSON Pointer path string"
    - "SchemaType is a Flags enum representing the 7 JSON Schema types"
    - "Boolean schemas (true/false) are representable via BooleanSchema field"
    - "Schema can be loaded from raw UTF-8 bytes via TryLoad, producing an immutable SchemaNode tree"
    - "Schema can be loaded from a JSON string via TryLoad, producing an immutable SchemaNode tree"
    - "Load returns nullable and never throws"
    - "TryLoad returns false for invalid JSON input"
    - "Every node in the schema tree has a precomputed RFC 6901 JSON Pointer path"
    - "Nested properties have correct paths (e.g., /address/street)"
    - "Each named property in the schema tree is assigned a stable depth-first integer ordinal"
    - "PropertyCount matches the number of named properties in the schema"
    - "Unknown keywords are silently ignored"
    - "Boolean schemas (true/false) are handled correctly (e.g., additionalProperties: false)"
  artifacts:
    - path: "src/Gluey.Contract/SchemaType.cs"
      status: verified
    - path: "src/Gluey.Contract/SchemaNode.cs"
      status: verified
    - path: "src/Gluey.Contract.Json/JsonSchemaLoader.cs"
      status: verified
    - path: "src/Gluey.Contract.Json/SchemaIndexer.cs"
      status: verified
    - path: "src/Gluey.Contract.Json/JsonContractSchema.cs"
      status: verified
    - path: "tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs"
      status: verified
    - path: "tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs"
      status: verified
  key_links:
    - from: "JsonSchemaLoader.cs"
      to: "SchemaNode.cs"
      via: "new SchemaNode("
      status: verified
    - from: "SchemaNode.cs"
      to: "SchemaType.cs"
      via: "SchemaType? Type property"
      status: verified
    - from: "JsonContractSchema.cs"
      to: "JsonSchemaLoader.cs"
      via: "JsonSchemaLoader.Load"
      status: verified
    - from: "JsonContractSchema.cs"
      to: "SchemaIndexer.cs"
      via: "SchemaIndexer.AssignOrdinals"
      status: verified
    - from: "SchemaIndexer.cs"
      to: "SchemaNode.cs"
      via: "node.Properties traversal"
      status: verified
requirements:
  - id: SCHM-01
    status: satisfied
  - id: SCHM-02
    status: satisfied
  - id: SCHM-05
    status: satisfied
---

# Phase 2: Schema Model Verification Report

**Phase Goal:** JSON Schema documents can be loaded and compiled into an immutable, indexed schema tree with precomputed paths ready for validation
**Verified:** 2026-03-09T12:55:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SchemaNode is an internal sealed class with all Draft 2020-12 keyword fields | VERIFIED | SchemaNode.cs: `internal sealed class SchemaNode` with ~45 get-only properties covering all keyword categories |
| 2 | SchemaNode is immutable -- all properties set in constructor, no public setters | VERIFIED | All properties are `{ get; }` only; single constructor assigns all fields (lines 193-304) |
| 3 | SchemaNode carries a precomputed RFC 6901 JSON Pointer path string | VERIFIED | `internal string Path { get; }` set at construction; BuildChildPath helper with RFC 6901 escaping (lines 315-322) |
| 4 | SchemaType is a Flags enum representing the 7 JSON Schema types | VERIFIED | SchemaType.cs: `[Flags] internal enum SchemaType : byte` with None, Null, Boolean, Integer, Number, String, Array, Object |
| 5 | Boolean schemas (true/false) are representable via BooleanSchema field | VERIFIED | `internal bool? BooleanSchema { get; }` plus `static readonly SchemaNode True/False` sentinels |
| 6 | Schema can be loaded from raw UTF-8 bytes via TryLoad | VERIFIED | `JsonContractSchema.TryLoad(ReadOnlySpan<byte>, out JsonContractSchema?)` delegates to JsonSchemaLoader.Load -> SchemaIndexer.AssignOrdinals |
| 7 | Schema can be loaded from a JSON string via TryLoad | VERIFIED | `JsonContractSchema.TryLoad(string, out JsonContractSchema?)` converts to UTF-8 bytes then delegates |
| 8 | Load returns nullable and never throws | VERIFIED | `JsonContractSchema.Load()` returns `JsonContractSchema?`; JsonSchemaLoader wraps in try/catch(JsonException) |
| 9 | TryLoad returns false for invalid JSON input | VERIFIED | Test covers "not json", "[]", "null", "" -- all return false. JsonSchemaLoader returns null for non-object/non-boolean tokens |
| 10 | Every node in the schema tree has a precomputed RFC 6901 JSON Pointer path | VERIFIED | ReadSchemaMap builds child paths via SchemaNode.BuildChildPath; root path is "" |
| 11 | Nested properties have correct paths (e.g., /address/street) | VERIFIED | Test SchemaNodeTests.Load_NestedProperties_ProducesCorrectPaths asserts /address/street and /address/city |
| 12 | Each named property is assigned a stable depth-first integer ordinal | VERIFIED | SchemaIndexer.AssignOrdinals walks depth-first; test asserts /name=0, /address=1, /address/street=2, /address/city=3 |
| 13 | PropertyCount matches the number of named properties | VERIFIED | JsonContractSchema.PropertyCount exposed; tests verify count=3 for 3 props, count=4 for nested |
| 14 | Unknown keywords are silently ignored | VERIFIED | JsonSchemaLoader else branch: `reader.Read(); reader.Skip()`. Tests confirm x-custom loads without error |
| 15 | Boolean schemas handled correctly (additionalProperties: false) | VERIFIED | ReadSchemaOrBoolean returns SchemaNode.False for boolean false token; test asserts BeSameAs(SchemaNode.False) |

**Score:** 15/15 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Gluey.Contract/SchemaType.cs` | Flags enum for JSON Schema type keyword | VERIFIED | 20 lines, [Flags] attribute present, 7 types + None |
| `src/Gluey.Contract/SchemaNode.cs` | Immutable schema tree node (min 80 lines) | VERIFIED | 323 lines, internal sealed class, ~45 get-only properties, single constructor |
| `src/Gluey.Contract.Json/JsonSchemaLoader.cs` | Recursive-descent parser (min 150 lines) | VERIFIED | 600 lines, handles all Draft 2020-12 keywords, zero-alloc u8 matching |
| `src/Gluey.Contract.Json/SchemaIndexer.cs` | Depth-first ordinal assignment (min 30 lines) | VERIFIED | 113 lines, walks all node types, assigns ordinals to Properties children only |
| `src/Gluey.Contract.Json/JsonContractSchema.cs` | TryLoad/Load factory methods, PropertyCount | VERIFIED | 149 lines, 4 factory methods (byte/string x TryLoad/Load), PropertyCount property, private constructor |
| `tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs` | Parser tests (min 80 lines) | VERIFIED | 569 lines, 36 tests covering all keyword categories |
| `tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs` | API and ordinal tests (min 80 lines) | VERIFIED | 225 lines, 18 tests covering TryLoad/Load API, PropertyCount, ordinals |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| JsonSchemaLoader.cs | SchemaNode.cs | `new SchemaNode(` constructor call | WIRED | Line 341: constructs SchemaNode with all keyword fields |
| SchemaNode.cs | SchemaType.cs | `SchemaType?` Type property | WIRED | Line 47: `internal SchemaType? Type { get; }` |
| JsonContractSchema.cs | JsonSchemaLoader.cs | `JsonSchemaLoader.Load` call | WIRED | Line 59: `var root = JsonSchemaLoader.Load(utf8Json)` |
| JsonContractSchema.cs | SchemaIndexer.cs | `SchemaIndexer.AssignOrdinals` call | WIRED | Line 66: `var (nameToOrdinal, propertyCount) = SchemaIndexer.AssignOrdinals(root)` |
| SchemaIndexer.cs | SchemaNode.cs | `node.Properties` traversal | WIRED | Lines 30-42: iterates Properties dictionary for ordinal assignment |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SCHM-01 | 02-02 | Schema loading from JSON bytes and JSON string | SATISFIED | TryLoad/Load factory methods accept both ReadOnlySpan<byte> and string; 59 Json tests pass |
| SCHM-02 | 02-01 | SchemaNode immutable tree with precomputed JSON Pointer paths | SATISFIED | SchemaNode internal sealed class, get-only properties, BuildChildPath with RFC 6901 escaping |
| SCHM-05 | 02-02 | Property index assignment for zero-allocation offset table sizing | SATISFIED | SchemaIndexer assigns depth-first ordinals; PropertyCount exposed for OffsetTable sizing |

No orphaned requirements found -- REQUIREMENTS.md maps exactly SCHM-01, SCHM-02, SCHM-05 to Phase 2, matching plan declarations.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| JsonContractSchema.cs | 124 | `TODO: Phase 9 -- implement single-pass walker` | Info | Expected stub for TryParse -- Phase 9 scope, not Phase 2 |
| JsonContractSchema.cs | 143 | `TODO: Phase 9 -- implement single-pass walker` | Info | Expected stub for Parse -- Phase 9 scope, not Phase 2 |

The TODO comments on TryParse/Parse are intentional Phase 9 placeholders, not Phase 2 gaps. The Phase 2 deliverables (TryLoad/Load) are fully implemented.

### Human Verification Required

None. All Phase 2 deliverables are verifiable programmatically via tests and code inspection.

### Test Results

- **Gluey.Contract.Tests:** 72 passed, 0 failed
- **Gluey.Contract.Json.Tests:** 59 passed, 0 failed
- **Total:** 131 passed, 0 failed

All Phase 1 tests remain green (no regressions).

### Gaps Summary

No gaps found. All 15 observable truths verified, all 7 artifacts pass all three verification levels (exists, substantive, wired), all 5 key links confirmed, and all 3 requirements satisfied. The phase goal -- loading JSON Schema documents into an immutable, indexed schema tree with precomputed paths -- is fully achieved.

---

_Verified: 2026-03-09T12:55:00Z_
_Verifier: Claude (gsd-verifier)_
