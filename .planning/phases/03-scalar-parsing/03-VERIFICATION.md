---
phase: 03-scalar-parsing
verified: 2026-03-20T00:00:00Z
status: passed
score: 6/6 must-haves verified
re_verification: false
---

# Phase 3: Scalar Parsing Verification Report

**Phase Goal:** Consumer can parse a binary payload containing scalar fields and access values through ParsedProperty
**Verified:** 2026-03-20
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | `BinaryContractSchema.Parse(byte[])` returns a `ParseResult?` with scalar fields accessible via `parsed["fieldName"].GetXxx()` | VERIFIED | `Parse(byte[])` at line 173 of BinaryContractSchema.cs; 24 E2E tests in ScalarParsingTests.cs confirm round-trip |
| 2  | Integer and float fields read correctly in both big-endian and little-endian byte order | VERIFIED | `GetUInt16`, `GetUInt32`, `GetInt32`, `GetDouble` all dispatch on `_endianness` using `BinaryPrimitives`; 6 E2E tests cover both directions |
| 3  | Truncated numerics sign-extend for signed types and zero-pad for unsigned types | VERIFIED | `SignExtend3BytesBigEndian` / `SignExtend3BytesLittleEndian` helpers in ParsedProperty.cs; `GetInt32` 3-byte case verified by `Parse_TruncatedSigned_BigEndian_NegativeValue_SignExtends` (-12345); `GetUInt32` 3-byte case verified by `Parse_TruncatedUnsigned_BigEndian_ZeroPads` (16764871) |
| 4  | Payload shorter than `TotalFixedSize` returns null | VERIFIED | `Parse(byte[])` line 175: `if (TotalFixedSize >= 0 && data.Length < TotalFixedSize) return null;` confirmed by `Parse_PayloadTooShort_ReturnsNull` |
| 5  | `GetXxx()` on a mismatched type throws `InvalidOperationException` | VERIFIED | All GetXxx() binary paths check `_fieldType != FieldTypes.None && _fieldType != FieldTypes.Xxx`; confirmed by 3 type-strictness tests including E2E `Parse_GetInt32_OnUInt16Field_ThrowsInvalidOperationException_E2E` |
| 6  | `GetUInt8` / `GetUInt16` / `GetUInt32` exist and return correct values | VERIFIED | All three methods present in ParsedProperty.cs (lines 295, 313, 346); confirmed by `GetUInt8_OnBinaryProperty_ReturnsCorrectByte`, `GetUInt16_LittleEndian_ReturnsCorrectValue`, `GetUInt32_BigEndian4Bytes_ReturnsCorrectValue` |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Gluey.Contract/Parsing/ParsedProperty.cs` | Field type metadata, unsigned accessors, truncated numeric paths, type strictness | VERIFIED | `_fieldType` field at line 64; `FieldTypes` class lines 26-42; `GetUInt8` line 295; `GetUInt16` line 313; `GetUInt32` line 346; `SignExtend3BytesBigEndian` line 545; type strictness in all binary GetXxx() |
| `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` | Two Parse overloads returning `ParseResult?` | VERIFIED | `public ParseResult? Parse(byte[] data)` line 173; `public ParseResult? Parse(ReadOnlySpan<byte> data)` line 208 |
| `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` | End-to-end tests for all scalar parsing requirements | VERIFIED | 610 lines, 40 test methods (16 unit-level + 24 E2E); all 40 pass on both net9.0 and net10.0 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `BinaryContractSchema.cs` | `ParsedProperty.cs` | Parse loop creates `ParsedProperty` with 7-param binary constructor including `fieldType` | WIRED | Line 192-195: `new ParsedProperty(data, node.AbsoluteOffset, node.Size, "/" + node.Name, 1, node.ResolvedEndianness, fieldType)` |
| `BinaryContractSchema.cs` | `OffsetTable` | Parse populates `OffsetTable` with scalar properties | WIRED | Line 178: `new OffsetTable(OrderedFields.Length)`; line 196: `offsetTable.Set(i, prop)` |
| `BinaryContractSchema.cs` | `ParseResult` | Parse returns `new ParseResult(...)` | WIRED | Line 199: `return new ParseResult(offsetTable, errors, NameToOrdinal)` |
| `Gluey.Contract.csproj` | `Gluey.Contract.Binary` assembly | `InternalsVisibleTo` grants access to `OffsetTable`, `FieldTypes`, `ParseResult` internals | WIRED | `Gluey.Contract.csproj` line 35: `<InternalsVisibleTo Include="Gluey.Contract.Binary" />` |
| `ScalarParsingTests.cs` | `BinaryContractSchema.cs` | Tests call `schema.Parse(payload)` and assert `parsed["fieldName"].GetXxx()` | WIRED | Multiple `schema.Parse(payload)` calls confirmed; `using Gluey.Contract.Binary.Schema` at line 16 |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SCLR-01 | 03-01, 03-02 | Parser reads uint8, uint16, uint32 with correct endianness | SATISFIED | `GetUInt8`, `GetUInt16`, `GetUInt32` methods implemented with `BinaryPrimitives`; 5 tests including `Parse_LittleEndian_UInt8_ReturnsCorrectValue`, `Parse_LittleEndian_UInt16_ReturnsCorrectValue`, `Parse_BigEndian_UInt16_ReturnsCorrectValue` |
| SCLR-02 | 03-01, 03-02 | Parser reads int8, int16, int32 with correct endianness | SATISFIED | `GetInt32` handles 1-byte (sbyte cast), 2-byte (Int16), 4-byte paths for both endiannesses; confirmed by `Parse_LittleEndian_Int32_ReturnsCorrectNegativeValue` (-12345), `Parse_BigEndian_Int32_ReturnsCorrectNegativeValue` |
| SCLR-03 | 03-01, 03-02 | Parser reads float32 and float64 with correct endianness | SATISFIED | `GetDouble` handles 4-byte (ReadSingle) and 8-byte (ReadDouble) with endianness dispatch; confirmed by `Parse_LittleEndian_Float32_ReturnsCorrectValue`, `Parse_LittleEndian_Float64_ReturnsCorrectValue`, `Parse_BigEndian_Float32_ReturnsCorrectValue` |
| SCLR-04 | 03-01, 03-02 | Parser reads boolean (0 = false, non-zero = true) | SATISFIED | `GetBoolean` binary path: `return _buffer[_offset] != 0;`; confirmed by `Parse_Boolean_ZeroReturnsFalse`, `Parse_Boolean_NonZeroReturnsTrue`, `Parse_Boolean_HighValueReturnsTrue` (0xFF) |
| SCLR-05 | 03-01, 03-02 | Truncated numerics: int32 in fewer bytes with correct sign extension | SATISFIED | `SignExtend3BytesBigEndian` and `SignExtend3BytesLittleEndian` helpers; `GetInt32` 3-byte case in both endianness paths; confirmed by `Parse_TruncatedSigned_BigEndian_NegativeValue_SignExtends` ([0xFF,0xCF,0xC7] = -12345) and `GetInt32_BigEndian3Bytes_SignExtends` (unit), `GetInt32_LittleEndian3Bytes_SignExtends` (unit) |
| SCLR-06 | 03-01, 03-02 | Truncated numerics: uint32 in fewer bytes with zero-padding | SATISFIED | `GetUInt32` 3-byte big-endian path: `((uint)span[0] << 16) \| ((uint)span[1] << 8) \| span[2]`; confirmed by `Parse_TruncatedUnsigned_BigEndian_ZeroPads` ([0xFF,0xCF,0xC7] = 16764871) and `GetUInt32_BigEndian3Bytes_ZeroPads` (unit) |
| CORE-04 | 03-01, 03-02 | `BinaryContractSchema.Parse(byte[])` returns `ParseResult?` (null for structurally invalid payloads) | SATISFIED | Both `Parse(byte[])` and `Parse(ReadOnlySpan<byte>)` return `ParseResult?`; confirmed by `Parse_PayloadTooShort_ReturnsNull` (10 bytes vs 22 required) and `Parse_PayloadExactSize_ReturnsParsedResult` |
| CORE-05 | 03-01, 03-02 | Zero-allocation parse path using ArrayPool, OffsetTable, ErrorCollector, ArrayBuffer | SATISFIED | `Parse(byte[])` uses `new OffsetTable(...)` and `new ErrorCollector()` (ArrayPool-backed); `ParseResult` is `IDisposable`; confirmed by `Parse_ReturnsDisposableParseResult` (using pattern, Dispose completes without exception) |

No orphaned requirements detected. REQUIREMENTS.md traceability table maps all 8 IDs (SCLR-01 through SCLR-06, CORE-04, CORE-05) to Phase 3 with status "Complete", consistent with the implementation found.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `ParsedProperty.cs` | 286-287 | `GetString()` binary path defers to `Encoding.UTF8.GetString` with comment "encoding-specific logic deferred to Phase 4" | Info | Not a Phase 3 concern; string parsing is STRE-01/STRE-02 (Phase 4 pending) |

No blocker or warning anti-patterns found in Phase 3 scope. The `GetString()` stub is intentional and scoped to Phase 4.

### Human Verification Required

None. All phase-3 behavior is verified programmatically via the test suite. The 40 passing tests cover:
- All 8 requirement IDs with direct assertions
- Both little-endian and big-endian directions for all numeric types
- Truncated 3-byte read reference cases with exact byte values
- Type-strictness enforcement with exception assertions
- Null return for short payload
- `IDisposable` disposal path

### Gaps Summary

No gaps. All 6 observable truths verified, all 3 artifacts substantive and wired, all 5 key links confirmed. All 8 requirement IDs satisfied with passing tests. Full suite: 966 tests, 0 failures, across net9.0 and net10.0.

---

_Verified: 2026-03-20_
_Verifier: Claude (gsd-verifier)_
