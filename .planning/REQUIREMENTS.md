# Requirements: Gluey.Contract.Binary

**Defined:** 2026-03-19
**Core Value:** A consumer calls parsed["fieldName"].GetInt32() and gets the value — without knowing or caring whether the backing data is JSON or a custom binary protocol.

## v1 Requirements

### Core Infrastructure

- [x] **CORE-01**: ParsedProperty has a 1-byte format flag that dispatches GetXxx() between UTF-8 and binary reading
- [x] **CORE-02**: Adding format flag does not break existing JSON consumers (all JSON tests pass unchanged)
- [x] **CORE-03**: BinaryContractSchema exposes TryLoad/Load static factory methods matching JsonContractSchema pattern
- [x] **CORE-04**: BinaryContractSchema.Parse(byte[]) returns ParseResult? (null for structurally invalid payloads)
- [x] **CORE-05**: Zero-allocation parse path using ArrayPool, OffsetTable, ErrorCollector, ArrayBuffer

### Contract Model

- [x] **CNTR-01**: Binary contract JSON loaded and parsed into internal model (BinaryContractNode tree)
- [x] **CNTR-02**: Dependency chain resolved at load time into ordered field array (no graph traversal at parse time)
- [x] **CNTR-03**: Contract-load validation: exactly one root field (no dependsOn)
- [x] **CNTR-04**: Contract-load validation: no cycles in dependency graph
- [x] **CNTR-05**: Contract-load validation: each field has at most one child
- [x] **CNTR-06**: Contract-load validation: semi-dynamic array count references valid numeric field earlier in chain
- [x] **CNTR-07**: Contract-load validation: bit sub-fields do not overlap and fit within container size
- [x] **CNTR-08**: Contract-load validation: size is explicitly declared on every field
- [x] **CNTR-09**: Endianness resolved at load time (contract-level default with per-field override)

### Scalar Parsing

- [x] **SCLR-01**: Parser reads uint8, uint16, uint32 with correct endianness via BinaryPrimitives
- [x] **SCLR-02**: Parser reads int8, int16, int32 with correct endianness via BinaryPrimitives
- [x] **SCLR-03**: Parser reads float32 and float64 with correct endianness
- [x] **SCLR-04**: Parser reads boolean (0 = false, non-zero = true)
- [x] **SCLR-05**: Truncated numerics: int32 in fewer bytes with correct sign extension
- [x] **SCLR-06**: Truncated numerics: uint32 in fewer bytes with zero-padding

### String and Enum

- [x] **STRE-01**: Parser reads fixed-length ASCII strings
- [x] **STRE-02**: Parser reads fixed-length UTF-8 strings
- [x] **STRE-03**: Enum field maps byte value to string via contract values table
- [x] **STRE-04**: Enum dual-access: parsed["name"] returns mapped string, parsed["names"] returns raw numeric

### Bit Fields

- [x] **BITS-01**: Bit container reads 1-2 bytes and extracts sub-fields at specified bit positions and widths
- [x] **BITS-02**: Boolean sub-fields (1-bit width) return true/false
- [x] **BITS-03**: Numeric sub-fields extract correct unsigned value across bit positions
- [x] **BITS-04**: Multi-byte bit containers (16 bits) work correctly with endianness

### Composite Types

- [x] **COMP-01**: Fixed arrays: count as number, parser reads N elements of specified type
- [x] **COMP-02**: Semi-dynamic arrays: count as string referencing another field, resolved at parse time
- [x] **COMP-03**: Struct elements inside arrays with scoped dependency chains (sub-field offsets relative to element start)
- [x] **COMP-04**: Padding fields: parser skips specified number of bytes, not exposed in ParsedObject
- [x] **COMP-05**: Path-based access: parsed["arrayName/0/fieldName"] works for nested struct array elements

### Validation

- [x] **VALD-01**: Numeric fields validated against min/max from contract
- [x] **VALD-02**: String fields validated against pattern (regex) from contract
- [x] **VALD-03**: String fields validated against minLength/maxLength from contract
- [x] **VALD-04**: Payload too short for fixed-size contract returns null
- [x] **VALD-05**: Multiple validation errors collected (not fail-fast), using ErrorCollector

### Packaging

- [x] **PACK-01**: Gluey.Contract.Binary NuGet package targeting net9.0 and net10.0
- [x] **PACK-02**: CI pipeline matching Gluey.Contract.Json (build, test, pack)
- [x] **PACK-03**: README with usage examples (load contract, parse payload, access values)
- [x] **PACK-04**: High code coverage with unit and integration tests
- [x] **PACK-05**: InternalsVisibleTo for test project

## v2 Requirements

### Extended Types

- **EXT-01**: Nested structs outside of array elements (standalone struct fields)
- **EXT-02**: Delimiter-terminated arrays
- **EXT-03**: Variable-length strings (length-prefixed)

### Serialization

- **SER-01**: Serialize ParsedObject back to byte[] given a contract
- **SER-02**: ToProtobuf() extension method for format conversion

### Advanced Validation

- **ADVL-01**: Cross-field validation (field A must be less than field B)
- **ADVL-02**: Conditional fields (field present only if flag is set)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Serialization (object -> byte[]) | Parse-only for v1; serialization needs different contract semantics |
| Fully dynamic arrays | No count or terminator -- impossible to parse reliably |
| Stream-based incremental parsing | Scope trap; byte[] input is sufficient for IoT payloads |
| Protobuf/MessagePack compatibility | This is for custom binary formats, not standard protocols |
| Schema generation from DSL | Belongs in Gluey compiler, not runtime library |
| Conditional/optional fields | Complexity trap; can be modeled with enums and validation |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | Phase 1 | Complete |
| CORE-02 | Phase 1 | Complete |
| CORE-03 | Phase 2 | Complete |
| CORE-04 | Phase 3 | Complete |
| CORE-05 | Phase 3 | Complete |
| CNTR-01 | Phase 2 | Complete |
| CNTR-02 | Phase 2 | Complete |
| CNTR-03 | Phase 2 | Complete |
| CNTR-04 | Phase 2 | Complete |
| CNTR-05 | Phase 2 | Complete |
| CNTR-06 | Phase 2 | Complete |
| CNTR-07 | Phase 2 | Complete |
| CNTR-08 | Phase 2 | Complete |
| CNTR-09 | Phase 2 | Complete |
| SCLR-01 | Phase 3 | Complete |
| SCLR-02 | Phase 3 | Complete |
| SCLR-03 | Phase 3 | Complete |
| SCLR-04 | Phase 3 | Complete |
| SCLR-05 | Phase 3 | Complete |
| SCLR-06 | Phase 3 | Complete |
| STRE-01 | Phase 4 | Complete |
| STRE-02 | Phase 4 | Complete |
| STRE-03 | Phase 4 | Complete |
| STRE-04 | Phase 4 | Complete |
| BITS-01 | Phase 4 | Complete |
| BITS-02 | Phase 4 | Complete |
| BITS-03 | Phase 4 | Complete |
| BITS-04 | Phase 4 | Complete |
| COMP-01 | Phase 5 | Complete |
| COMP-02 | Phase 5 | Complete |
| COMP-03 | Phase 5 | Complete |
| COMP-04 | Phase 4 | Complete |
| COMP-05 | Phase 5 | Complete |
| VALD-01 | Phase 6 | Complete |
| VALD-02 | Phase 6 | Complete |
| VALD-03 | Phase 6 | Complete |
| VALD-04 | Phase 6 | Complete |
| VALD-05 | Phase 6 | Complete |
| PACK-01 | Phase 7 | Complete |
| PACK-02 | Phase 7 | Complete |
| PACK-03 | Phase 7 | Complete |
| PACK-04 | Phase 7 | Complete |
| PACK-05 | Phase 7 | Complete |

**Coverage:**
- v1 requirements: 43 total
- Mapped to phases: 43
- Unmapped: 0

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-19 after roadmap creation*
