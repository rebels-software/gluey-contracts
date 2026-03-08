---
phase: 01-core-types
verified: 2026-03-08T23:30:00Z
status: passed
score: 23/23 must-haves verified
re_verification: false
---

# Phase 1: Core Types Verification Report

**Phase Goal:** All foundational value types exist as readonly structs with correct shapes, enabling downstream phases to produce and consume them without layout changes
**Verified:** 2026-03-08T23:30:00Z
**Status:** passed
**Re-verification:** No -- initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | ParsedProperty can hold offset, length, and path into a byte buffer and report HasValue correctly | VERIFIED | readonly struct with `_buffer`, `_offset`, `_length`, `_path` fields; `HasValue => _length > 0`; `Path => _path ?? string.Empty` |
| 2 | ParsedProperty materializes values via GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal() from raw UTF-8 bytes | VERIFIED | All 6 methods implemented with `Utf8Parser.TryParse` (numerics) and `Encoding.UTF8.GetString` (string); `[AggressiveInlining]` applied |
| 3 | ParsedProperty.RawBytes exposes the underlying bytes as ReadOnlySpan<byte> | VERIFIED | `RawBytes => _buffer.AsSpan(_offset, _length)` with null guard |
| 4 | Type mismatch on GetX() returns default(T) without exceptions | VERIFIED | All methods guard with `if (_length == 0) return default`; `Utf8Parser.TryParse` returns default on failure; tests confirm |
| 5 | ValidationError carries an RFC 6901 JSON Pointer path, a ValidationErrorCode, and a static message string | VERIFIED | `readonly struct ValidationError` with `Path`, `Code`, `Message` public readonly fields |
| 6 | ValidationErrorCode enum covers all JSON Schema Draft 2020-12 keywords (~28 values) plus TooManyErrors sentinel | VERIFIED | 36 enum values (byte-backed), covering type, enum/const, object, array, numeric, string, size, composition, conditional, advanced, format, and sentinel |
| 7 | ValidationErrorMessages returns a pre-allocated static string for every error code | VERIFIED | Static constructor populates `string[]` array; `Get()` returns `Messages[(int)code] ?? string.Empty`; test confirms every code except None has a message |
| 8 | OffsetTable maps property ordinals to ParsedProperty values using ArrayPool-backed storage | VERIFIED | `ArrayPool<ParsedProperty>.Shared.Rent(capacity)` in constructor; `Set(int ordinal, ParsedProperty)` and `this[int ordinal]` indexer |
| 9 | OffsetTable returns its ArrayPool buffer on Dispose | VERIFIED | `ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true)` with null guard |
| 10 | OffsetTable supports ordinal-based indexing to retrieve ParsedProperty entries | VERIFIED | `this[int ordinal]` with bounds check `(uint)ordinal < (uint)_capacity`, returns `ParsedProperty.Empty` on out-of-range |
| 11 | ErrorCollector pre-allocates a fixed-capacity buffer (default 64) from ArrayPool | VERIFIED | `DefaultCapacity = 64`; `ArrayPool<ValidationError>.Shared.Rent(capacity)` in constructor |
| 12 | ErrorCollector collects ValidationError instances without heap allocation | VERIFIED | `Add()` stores into pre-rented array; count tracked via `int[1]` (one-time allocation at construction) |
| 13 | When ErrorCollector hits max capacity, the last slot is replaced with a TooManyErrors sentinel | VERIFIED | `if (count == _capacity - 1)` replaces with sentinel using `ValidationErrorMessages.Get(TooManyErrors)`; beyond capacity silently dropped |
| 14 | ErrorCollector returns its ArrayPool buffer on Dispose | VERIFIED | `ArrayPool<ValidationError>.Shared.Return(_errors, clearArray: true)` with null guard |
| 15 | ParseResult exposes success/failure state via IsValid property | VERIFIED | `IsValid => !_errorCollector.HasErrors` |
| 16 | ParseResult supports string indexer (result["name"]) that resolves to a ParsedProperty | VERIFIED | `this[string name]` uses `_nameToOrdinal.TryGetValue` then delegates to `_offsetTable[ordinal]` |
| 17 | ParseResult supports ordinal indexer (result[0]) that resolves to a ParsedProperty | VERIFIED | `this[int ordinal] => _offsetTable[ordinal]` |
| 18 | Missing/absent property returns an empty ParsedProperty (HasValue == false) -- no exceptions | VERIFIED | Both indexers return `ParsedProperty.Empty` for unknown names and out-of-range ordinals; tests confirm |
| 19 | ParseResult.Errors is always accessible -- empty on success, populated on failure | VERIFIED | `Errors => _errorCollector`; tests verify Count==0 when valid, Count>0 when invalid |
| 20 | ParseResult supports foreach enumeration of all parsed properties via GetEnumerator() | VERIFIED | Custom `Enumerator` struct iterates `_offsetTable[0..Count)` skipping empty slots |
| 21 | ParseResult implements IDisposable and cascades disposal to OffsetTable and ErrorCollector | VERIFIED | `Dispose()` calls `_offsetTable.Dispose()` and `_errorCollector.Dispose()` |
| 22 | TryParse method signature compiles: bool TryParse(..., out ParseResult result) | VERIFIED | `public bool TryParse(ReadOnlySpan<byte> data, out ParseResult result)` compiles; test invokes it |
| 23 | Parse method signature compiles: ParseResult? Parse(...) and never throws | VERIFIED | `public ParseResult? Parse(ReadOnlySpan<byte> data)` compiles; test confirms no throw |

**Score:** 23/23 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Gluey.Contract/ParsedProperty.cs` | Zero-allocation byte buffer accessor (min 60 lines) | VERIFIED | 118 lines, full implementation with 6 GetX() methods |
| `src/Gluey.Contract/ValidationErrorCode.cs` | Machine-readable error code enum (min 30 lines) | VERIFIED | 142 lines, 36 byte-backed enum values |
| `src/Gluey.Contract/ValidationError.cs` | Readonly struct with path, code, message (min 15 lines) | VERIFIED | 29 lines, readonly struct with constructor |
| `src/Gluey.Contract/ValidationErrorMessages.cs` | Static message lookup (min 30 lines) | VERIFIED | 82 lines, static string[] with Get() method |
| `src/Gluey.Contract/OffsetTable.cs` | ArrayPool-backed ordinal mapping with IDisposable (min 40 lines) | VERIFIED | 84 lines, full implementation |
| `src/Gluey.Contract/ErrorCollector.cs` | Pre-allocated error buffer with sentinel overflow (min 50 lines) | VERIFIED | 149 lines, full implementation with struct Enumerator |
| `src/Gluey.Contract/ParseResult.cs` | Composite result with dual indexers, IDisposable, enumerator (min 80 lines) | VERIFIED | 128 lines, full implementation |
| `src/Gluey.Contract.Json/JsonContractSchema.cs` | Dual API surface with TryParse and Parse (min 20 lines) | VERIFIED | 57 lines, stub implementations (by design -- Phase 9) |
| `tests/Gluey.Contract.Tests/ParsedPropertyTests.cs` | Unit tests for ParsedProperty (min 50 lines) | VERIFIED | 170 lines, 22 tests |
| `tests/Gluey.Contract.Tests/ValidationErrorTests.cs` | Unit tests for ValidationError types (min 30 lines) | VERIFIED | 100 lines, 11 tests |
| `tests/Gluey.Contract.Tests/OffsetTableTests.cs` | Unit tests for OffsetTable (min 40 lines) | VERIFIED | 93 lines, 9 tests |
| `tests/Gluey.Contract.Tests/ErrorCollectorTests.cs` | Unit tests for ErrorCollector (min 50 lines) | VERIFIED | 171 lines, 13 tests |
| `tests/Gluey.Contract.Tests/ParseResultTests.cs` | Unit tests for ParseResult (min 60 lines) | VERIFIED | 198 lines, 14 tests |
| `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` | API shape/compilation tests (min 20 lines) | VERIFIED | 73 lines, 5 tests |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `ValidationError.cs` | `ValidationErrorCode.cs` | `Code` field typed as `ValidationErrorCode` | WIRED | `public readonly ValidationErrorCode Code;` on line 12 |
| `ValidationError.cs` | `ValidationErrorMessages.cs` | Message sourced from static lookup | WIRED | Constructor accepts message string; ErrorCollector uses `ValidationErrorMessages.Get()` to create sentinel |
| `OffsetTable.cs` | `ParsedProperty.cs` | Stores `ParsedProperty[]` entries | WIRED | `ParsedProperty[]? _entries` field, `ArrayPool<ParsedProperty>.Shared.Rent()`, indexer returns `ParsedProperty` |
| `ErrorCollector.cs` | `ValidationError.cs` | Stores `ValidationError[]` errors | WIRED | `ValidationError[]? _errors` field, `ArrayPool<ValidationError>.Shared.Rent()`, `Add(ValidationError)` |
| `ErrorCollector.cs` | `ValidationErrorMessages.cs` | Creates TooManyErrors sentinel | WIRED | `ValidationErrorMessages.Get(ValidationErrorCode.TooManyErrors)` on line 68 |
| `ParseResult.cs` | `OffsetTable.cs` | Wraps OffsetTable for property access | WIRED | `OffsetTable _offsetTable` field, both indexers delegate to it, `Dispose()` cascades |
| `ParseResult.cs` | `ErrorCollector.cs` | Wraps ErrorCollector for error access | WIRED | `ErrorCollector _errorCollector` field, `IsValid` and `Errors` properties use it, `Dispose()` cascades |
| `JsonContractSchema.cs` | `ParseResult.cs` | Returns ParseResult from TryParse/Parse | WIRED | `out ParseResult result` in TryParse, `ParseResult?` return type in Parse, `using Gluey.Contract;` import |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| CORE-01 | 01-01 | ParsedProperty readonly struct with offset, length, and path | SATISFIED | `ParsedProperty` readonly struct with `_buffer`, `_offset`, `_length`, `_path` fields and `internal` constructor |
| CORE-02 | 01-01 | On-demand value materialization via GetString/GetInt32/GetInt64/GetDouble/GetBoolean/GetDecimal | SATISFIED | All 6 methods implemented with `Utf8Parser` and `Encoding.UTF8`; `[AggressiveInlining]` applied |
| CORE-03 | 01-02 | Offset table mapping schema property ordinals to byte positions (ArrayPool-backed) | SATISFIED | `OffsetTable` readonly struct with `ArrayPool<ParsedProperty>.Shared.Rent()`, ordinal-based Set/indexer |
| CORE-04 | 01-01 | ValidationError readonly struct with RFC 6901 path, error code enum, and static message | SATISFIED | `ValidationError` readonly struct + `ValidationErrorCode : byte` enum (36 values) + `ValidationErrorMessages` static lookup |
| CORE-05 | 01-02 | ErrorCollector with pre-allocated buffer, max 64 errors default | SATISFIED | `ErrorCollector` readonly struct, `DefaultCapacity = 64`, `ArrayPool<ValidationError>` backing, sentinel overflow |
| CORE-06 | 01-03 | ParseResult readonly struct with success/failure and parsed data access, IDisposable | SATISFIED | `ParseResult : IDisposable` with `IsValid`, dual indexers, `Errors`, `GetEnumerator()`, cascading `Dispose()` |
| CORE-07 | 01-03 | Dual API surface: TryParse (bool + out) and Parse (returns nullable, never throws) | SATISFIED | `JsonContractSchema.TryParse(ReadOnlySpan<byte>, out ParseResult)` and `Parse(ReadOnlySpan<byte>): ParseResult?` -- stubs by design, full impl in Phase 9 |

No orphaned requirements found. All 7 CORE requirements mapped to Phase 1 in REQUIREMENTS.md are claimed by plans and satisfied.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `JsonContractSchema.cs` | 33 | `TODO: Phase 9 -- implement single-pass walker` | Info | Expected -- stub by design for this phase. TryParse returns false, Parse returns null. |
| `JsonContractSchema.cs` | 52 | `TODO: Phase 9 -- implement single-pass walker` | Info | Expected -- stub by design for this phase. Real implementation planned for Phase 9. |

No blocker or warning-level anti-patterns found.

### Human Verification Required

No human verification items identified. All truths are verifiable via code inspection and automated tests. The types are foundational data structures with no UI, external service, or real-time behavior to verify.

### Tests Summary

- **Total tests:** 77 (72 in Gluey.Contract.Tests + 5 in Gluey.Contract.Json.Tests)
- **Passed:** 77
- **Failed:** 0
- **Build:** 0 warnings, 0 errors

---

_Verified: 2026-03-08T23:30:00Z_
_Verifier: Claude (gsd-verifier)_
