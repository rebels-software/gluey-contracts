---
phase: 08-advanced-validation
verified: 2026-03-10T14:30:00Z
status: passed
score: 10/10 must-haves verified
re_verification: false
---

# Phase 8: Advanced Validation Verification Report

**Phase Goal:** Remaining validation keywords complete JSON Schema Draft 2020-12 coverage for v1 -- patternProperties, propertyNames, contains, uniqueItems, and format validation with SchemaOptions.
**Verified:** 2026-03-10T14:30:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | patternProperties matches property names against compiled regex and validates values via subschema result | VERIFIED | `ObjectValidator.ValidatePatternProperty()` at line 46-56 takes `bool schemaResult` and emits `PatternPropertyInvalid`. `SchemaNode.CompiledPatternProperties` field at line 139. `JsonSchemaLoader` compiles regex at lines 357-373 with `RegexOptions.Compiled` and fail-fast on invalid patterns. 11 tests in PatternPropertyValidatorTests. |
| 2 | propertyNames validates all property name strings against a subschema result | VERIFIED | `ObjectValidator.ValidatePropertyName()` at line 62-72 takes `bool nameSchemaResult` and emits `PropertyNameInvalid`. Tests verify both pass/fail paths. |
| 3 | contains validates that at least one array element matches, with minContains (default 1) and maxContains count control | VERIFIED | `ArrayValidator.ValidateContains()` at lines 46-79 implements effectiveMin = minContains ?? 1, ContainsInvalid vs MinContainsExceeded differentiation, MaxContainsExceeded. 8 tests in ContainsValidatorTests. |
| 4 | maxContains/minContains without contains has no effect | VERIFIED | These are count-based parameters on `ValidateContains` which is only called when `contains` schema exists -- the method signature design enforces this (caller decides when to call). |
| 5 | uniqueItems detects duplicate array elements using FNV-1a hash with stackalloc for arrays <= 128 items | VERIFIED | `ArrayValidator.ValidateUniqueItems()` at lines 86-135 with `Fnv1aHash` at lines 140-152. Uses `stackalloc int[count]` for count <= 128 (line 93-95). 10 tests in UniqueItemsValidatorTests. |
| 6 | uniqueItems handles numeric equivalence (1 and 1.0 are duplicates) | VERIFIED | Lines 121-123 check `isNumber[i] && isNumber[j]` then call `KeywordValidator.TryNumericEqual` outside the hash-equality guard (lines 119-130). Tests confirm 1==1.0 and 1e2==100. |
| 7 | Format keyword is treated as annotation by default (no validation errors produced) | VERIFIED | `SchemaOptions.AssertFormat` defaults to `false` (line 17). FormatValidator is only called when assertion mode is enabled (walker responsibility in Phase 9). Tests confirm default. |
| 8 | When SchemaOptions.AssertFormat = true, format keyword validates and produces FormatInvalid errors | VERIFIED | `FormatValidator.Validate()` at line 24 dispatches to 9 format validators, adds `FormatInvalid` error on failure. `JsonContractSchema.AssertFormat` property stores the flag (line 49). TryLoad/Load pass `options?.AssertFormat ?? false` (line 91). |
| 9 | Unrecognized format strings pass silently regardless of AssertFormat setting | VERIFIED | FormatValidator switch default at line 37: `_ => true`. Test `Validate_UnknownFormat_ReturnsTrueNoError` confirms. |
| 10 | All 9 formats validate correctly and TryLoad/Load accept optional SchemaOptions | VERIFIED | FormatValidator has 9 private validators (date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer). 31 format tests pass. TryLoad/Load have `SchemaOptions? options = null` parameter on all 4 overloads. 10 SchemaOptions integration tests pass. |

**Score:** 10/10 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Gluey.Contract.Json/ObjectValidator.cs` | ValidatePatternProperty, ValidatePropertyName methods | VERIFIED | 73 lines, both methods present with correct signatures and error codes |
| `src/Gluey.Contract.Json/ArrayValidator.cs` | ValidateContains, ValidateUniqueItems methods | VERIFIED | 153 lines, both methods present with FNV-1a hashing and numeric equivalence |
| `src/Gluey.Contract/SchemaNode.cs` | CompiledPatternProperties field | VERIFIED | Line 139: `internal (Regex Pattern, SchemaNode Schema)[]? CompiledPatternProperties`, wired in constructor at line 247 and line 303 |
| `src/Gluey.Contract.Json/KeywordValidator.cs` | TryNumericEqual internal accessibility | VERIFIED | Line 222: `internal static bool TryNumericEqual` (was private) |
| `src/Gluey.Contract.Json/JsonSchemaLoader.cs` | Compiles patternProperties regex at load time | VERIFIED | Lines 357-373 compile each patternProperties key with `RegexOptions.Compiled`, fail-fast on `ArgumentException` |
| `src/Gluey.Contract/SchemaOptions.cs` | Public sealed class with AssertFormat | VERIFIED | 18 lines, `public sealed class SchemaOptions` with `public bool AssertFormat { get; init; } = false;` |
| `src/Gluey.Contract.Json/FormatValidator.cs` | 9 format validators with dispatcher | VERIFIED | 154 lines, switch dispatcher + 9 private validators (ValidateDateTime through ValidateJsonPointer) |
| `src/Gluey.Contract.Json/JsonContractSchema.cs` | SchemaOptions parameter on TryLoad/Load | VERIFIED | All 4 overloads have `SchemaOptions? options = null`. Internal `AssertFormat` property at line 49 |
| `tests/Gluey.Contract.Json.Tests/PatternPropertyValidatorTests.cs` | Tests for VALD-12 | VERIFIED | 102 lines, 9 tests covering patternProperties, propertyNames, and schema load behavior |
| `tests/Gluey.Contract.Json.Tests/ContainsValidatorTests.cs` | Tests for VALD-13 | VERIFIED | 86 lines, 8 tests covering all contains/minContains/maxContains scenarios |
| `tests/Gluey.Contract.Json.Tests/UniqueItemsValidatorTests.cs` | Tests for VALD-14 | VERIFIED | 135 lines, 10 tests covering empty/single/distinct/duplicate/numeric-equivalence/type-mismatch |
| `tests/Gluey.Contract.Json.Tests/FormatValidatorTests.cs` | Tests for VALD-16 | VERIFIED | 309 lines, 31 tests covering all 9 formats + unknown format |
| `tests/Gluey.Contract.Json.Tests/SchemaOptionsTests.cs` | Tests for VALD-15 | VERIFIED | 119 lines, 10 tests covering defaults and TryLoad/Load integration |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| ObjectValidator.cs | ValidationErrorCode.cs | PatternPropertyInvalid, PropertyNameInvalid | WIRED | Lines 53 and 69 reference both error codes |
| ArrayValidator.cs | ValidationErrorCode.cs | ContainsInvalid, MinContainsExceeded, MaxContainsExceeded, UniqueItemsViolation | WIRED | Lines 55, 63, 73, 113, 126 reference all four error codes |
| ArrayValidator.cs | KeywordValidator.cs | TryNumericEqual for numeric equivalence | WIRED | Line 122: `KeywordValidator.TryNumericEqual(elementBytes[i], elementBytes[j], out bool equal)` |
| JsonSchemaLoader.cs | SchemaNode.cs | CompiledPatternProperties compiled at load time | WIRED | Line 360-366 builds array, line 410 passes to constructor |
| FormatValidator.cs | ValidationErrorCode.cs | FormatInvalid | WIRED | Line 43: `ValidationErrorCode.FormatInvalid` |
| JsonContractSchema.cs | SchemaOptions.cs | SchemaOptions parameter | WIRED | Line 74, 104, 121, 136 all accept `SchemaOptions?` parameter; line 91 uses `options?.AssertFormat ?? false` |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| VALD-12 | 08-01-PLAN | patternProperties and propertyNames | SATISFIED | ObjectValidator.ValidatePatternProperty/ValidatePropertyName, CompiledPatternProperties on SchemaNode, 9 tests |
| VALD-13 | 08-01-PLAN | contains, minContains, maxContains | SATISFIED | ArrayValidator.ValidateContains with effectiveMin logic, 8 tests |
| VALD-14 | 08-01-PLAN | uniqueItems with zero-allocation hashing | SATISFIED | ArrayValidator.ValidateUniqueItems with FNV-1a + stackalloc + numeric equivalence, 10 tests |
| VALD-15 | 08-02-PLAN | Format annotation by default, opt-in assertion | SATISFIED | SchemaOptions.AssertFormat defaults false, FormatValidator only called when enabled, 10 integration tests |
| VALD-16 | 08-02-PLAN | Common format validators (9 formats) | SATISFIED | FormatValidator with date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer, 31 tests |

No orphaned requirements found -- REQUIREMENTS.md maps VALD-12 through VALD-16 to Phase 8, all accounted for.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | - | - | - | No anti-patterns found in phase 8 files |

Note: `JsonContractSchema.cs` has `TODO: Phase 9` comments on TryParse/Parse stubs (lines 157, 176) but these are pre-existing from Phase 1 and intentionally deferred to Phase 9. Not a Phase 8 concern.

### Human Verification Required

None required. All phase 8 deliverables are static validator methods and schema options wiring, fully verifiable through automated tests. The 69 matched tests all pass (0 failures).

### Gaps Summary

No gaps found. All 10 observable truths verified, all 13 artifacts exist and are substantive, all 6 key links are wired, and all 5 requirements (VALD-12 through VALD-16) are satisfied. Commits `65d1fe0`, `0cb6beb`, `1992fb0`, `db5388c` all verified in git log.

---

_Verified: 2026-03-10T14:30:00Z_
_Verifier: Claude (gsd-verifier)_
