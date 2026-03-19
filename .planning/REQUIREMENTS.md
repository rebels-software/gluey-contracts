# Requirements: Gluey.Contract.Binary

**Defined:** 2026-03-19
**Core Value:** A consumer calls parsed["fieldName"].GetInt32() and gets the value — without knowing or caring whether the backing data is JSON or a custom binary protocol.

## v1 Requirements

### Core Infrastructure

- [ ] **CORE-01**: ParsedProperty has a 1-byte format flag that dispatches GetXxx() between UTF-8 and binary reading
- [ ] **CORE-02**: Adding format flag does not break existing JSON consumers (all JSON tests pass unchanged)
- [ ] **CORE-03**: BinaryContractSchema exposes TryLoad/Load static factory methods matching JsonContractSchema pattern
- [ ] **CORE-04**: BinaryContractSchema.Parse(byte[]) returns ParseResult? (null for structurally invalid payloads)
- [ ] **CORE-05**: Zero-allocation parse path using ArrayPool, OffsetTable, ErrorCollector, ArrayBuffer

### Contract Model

- [ ] **CNTR-01**: Binary contract JSON loaded and parsed into internal model (BinaryContractNode tree)
- [ ] **CNTR-02**: Dependency chain resolved at load time into ordered field array (no graph traversal at parse time)
- [ ] **CNTR-03**: Contract-load validation: exactly one root field (no dependsOn)
- [ ] **CNTR-04**: Contract-load validation: no cycles in dependency graph
- [ ] **CNTR-05**: Contract-load validation: each field has at most one child
- [ ] **CNTR-06**: Contract-load validation: semi-dynamic array count references valid numeric field earlier in chain
- [ ] **CNTR-07**: Contract-load validation: bit sub-fields do not overlap and fit within container size
- [ ] **CNTR-08**: Contract-load validation: size is explicitly declared on every field
- [ ] **CNTR-09**: Endianness resolved at load time (contract-level default with per-field override)

### Scalar Parsing

- [ ] **SCLR-01**: Parser reads uint8, uint16, uint32 with correct endianness via BinaryPrimitives
- [ ] **SCLR-02**: Parser reads int8, int16, int32 with correct endianness via BinaryPrimitives
- [ ] **SCLR-03**: Parser reads float32 and float64 with correct endianness
- [ ] **SCLR-04**: Parser reads boolean (0 = false, non-zero = true)
- [ ] **SCLR-05**: Truncated numerics: int32 in fewer bytes with correct sign extension
- [ ] **SCLR-06**: Truncated numerics: uint32 in fewer bytes with zero-padding

### String and Enum

- [ ] **STRE-01**: Parser reads fixed-length ASCII strings
- [ ] **STRE-02**: Parser reads fixed-length UTF-8 strings
- [ ] **STRE-03**: Enum field maps byte value to string via contract values table
- [ ] **STRE-04**: Enum dual-access: parsed["name"] returns mapped string, parsed["names"] returns raw numeric

### Bit Fields

- [ ] **BITS-01**: Bit container reads 1-2 bytes and extracts sub-fields at specified bit positions and widths
- [ ] **BITS-02**: Boolean sub-fields (1-bit width) return true/false
- [ ] **BITS-03**: Numeric sub-fields extract correct unsigned value across bit positions
- [ ] **BITS-04**: Multi-byte bit containers (16 bits) work correctly with endianness

### Composite Types

- [ ] **COMP-01**: Fixed arrays: count as number, parser reads N elements of specified type
- [ ] **COMP-02**: Semi-dynamic arrays: count as string referencing another field, resolved at parse time
- [ ] **COMP-03**: Struct elements inside arrays with scoped dependency chains (sub-field offsets relative to element start)
- [ ] **COMP-04**: Padding fields: parser skips specified number of bytes, not exposed in ParsedObject
- [ ] **COMP-05**: Path-based access: parsed["arrayName/0/fieldName"] works for nested struct array elements

### Validation

- [ ] **VALD-01**: Numeric fields validated against min/max from contract
- [ ] **VALD-02**: String fields validated against pattern (regex) from contract
- [ ] **VALD-03**: String fields validated against minLength/maxLength from contract
- [ ] **VALD-04**: Payload too short for fixed-size contract returns null
- [ ] **VALD-05**: Multiple validation errors collected (not fail-fast), using ErrorCollector

### Packaging

- [ ] **PACK-01**: Gluey.Contract.Binary NuGet package targeting net9.0 and net10.0
- [ ] **PACK-02**: CI pipeline matching Gluey.Contract.Json (build, test, pack)
- [ ] **PACK-03**: README with usage examples (load contract, parse payload, access values)
- [ ] **PACK-04**: High code coverage with unit and integration tests
- [ ] **PACK-05**: InternalsVisibleTo for test project

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
| Serialization (object → byte[]) | Parse-only for v1; serialization needs different contract semantics |
| Fully dynamic arrays | No count or terminator — impossible to parse reliably |
| Stream-based incremental parsing | Scope trap; byte[] input is sufficient for IoT payloads |
| Protobuf/MessagePack compatibility | This is for custom binary formats, not standard protocols |
| Schema generation from DSL | Belongs in Gluey compiler, not runtime library |
| Conditional/optional fields | Complexity trap; can be modeled with enums and validation |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | — | Pending |
| CORE-02 | — | Pending |
| CORE-03 | — | Pending |
| CORE-04 | — | Pending |
| CORE-05 | — | Pending |
| CNTR-01 | — | Pending |
| CNTR-02 | — | Pending |
| CNTR-03 | — | Pending |
| CNTR-04 | — | Pending |
| CNTR-05 | — | Pending |
| CNTR-06 | — | Pending |
| CNTR-07 | — | Pending |
| CNTR-08 | — | Pending |
| CNTR-09 | — | Pending |
| SCLR-01 | — | Pending |
| SCLR-02 | — | Pending |
| SCLR-03 | — | Pending |
| SCLR-04 | — | Pending |
| SCLR-05 | — | Pending |
| SCLR-06 | — | Pending |
| STRE-01 | — | Pending |
| STRE-02 | — | Pending |
| STRE-03 | — | Pending |
| STRE-04 | — | Pending |
| BITS-01 | — | Pending |
| BITS-02 | — | Pending |
| BITS-03 | — | Pending |
| BITS-04 | — | Pending |
| COMP-01 | — | Pending |
| COMP-02 | — | Pending |
| COMP-03 | — | Pending |
| COMP-04 | — | Pending |
| COMP-05 | — | Pending |
| VALD-01 | — | Pending |
| VALD-02 | — | Pending |
| VALD-03 | — | Pending |
| VALD-04 | — | Pending |
| VALD-05 | — | Pending |
| PACK-01 | — | Pending |
| PACK-02 | — | Pending |
| PACK-03 | — | Pending |
| PACK-04 | — | Pending |
| PACK-05 | — | Pending |

**Coverage:**
- v1 requirements: 43 total
- Mapped to phases: 0
- Unmapped: 43 ⚠️

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-19 after initial definition*
