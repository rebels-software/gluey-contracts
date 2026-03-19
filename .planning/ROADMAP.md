# Roadmap: Gluey.Contract.Binary

## Overview

This roadmap delivers a binary protocol parsing package that mirrors the existing JSON contract system. The build order follows the dependency chain: modify the shared ParsedProperty struct first, then build the contract model and loader, then implement parsing from simple types to complex composites, layer validation on top, and finally package for release. Each phase produces independently testable output.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Format Flag** - Add binary format discriminator to ParsedProperty without breaking JSON consumers
- [ ] **Phase 2: Contract Model** - Load, validate, and resolve binary contract JSON into an ordered field descriptor array
- [ ] **Phase 3: Scalar Parsing** - First end-to-end parse pipeline reading scalar fields from binary payloads
- [ ] **Phase 4: Leaf Types** - Strings, enums, bit fields, and padding complete the non-composite field types
- [ ] **Phase 5: Composite Types** - Arrays and nested structs with path-based access
- [ ] **Phase 6: Validation** - Contract-driven min/max, pattern, and length validation with error collection
- [ ] **Phase 7: Packaging** - NuGet package, CI pipeline, integration tests, and documentation

## Phase Details

### Phase 1: Format Flag
**Goal**: ParsedProperty dispatches between JSON and binary reading paths without breaking any existing consumer
**Depends on**: Nothing (first phase)
**Requirements**: CORE-01, CORE-02
**Success Criteria** (what must be TRUE):
  1. ParsedProperty has a format flag that distinguishes binary from JSON backing data
  2. All existing JSON package tests pass without modification after the struct change
  3. Binary-path branches exist in GetInt32, GetDouble, GetBoolean, GetString (can be stub/throw for now)
**Plans**: TBD

Plans:
- [ ] 01-01: TBD

### Phase 2: Contract Model
**Goal**: A binary contract JSON file can be loaded, structurally validated, and resolved into an ordered field array ready for the parser
**Depends on**: Phase 1
**Requirements**: CNTR-01, CNTR-02, CNTR-03, CNTR-04, CNTR-05, CNTR-06, CNTR-07, CNTR-08, CNTR-09, CORE-03
**Success Criteria** (what must be TRUE):
  1. BinaryContractSchema.TryLoad/Load reads a contract JSON file and returns a schema object (or error) matching the JsonContractSchema API pattern
  2. Contract with a cycle, missing root, shared parent, invalid ref, overlapping bits, or missing size is rejected with a clear error at load time
  3. Dependency chain is resolved into an ordered field array with precomputed endianness per field (no graph traversal at parse time)
  4. Contract model captures all ADR-16 field types (scalars, strings, enums, bit fields, arrays, structs, padding)
**Plans**: TBD

Plans:
- [ ] 02-01: TBD

### Phase 3: Scalar Parsing
**Goal**: Consumer can parse a binary payload containing scalar fields and access values through ParsedProperty
**Depends on**: Phase 2
**Requirements**: SCLR-01, SCLR-02, SCLR-03, SCLR-04, SCLR-05, SCLR-06, CORE-04, CORE-05
**Success Criteria** (what must be TRUE):
  1. BinaryContractSchema.Parse(byte[]) returns a ParseResult with scalar fields accessible via parsed["fieldName"].GetXxx()
  2. Integer and float fields read correctly in both big-endian and little-endian byte order
  3. Truncated numerics (e.g., int32 in 3 bytes) sign-extend correctly for signed types and zero-pad for unsigned types
  4. Payload shorter than the contract's fixed size returns null (not an exception)
  5. Parse path uses ArrayPool with no heap allocations on the hot path
**Plans**: TBD

Plans:
- [ ] 03-01: TBD

### Phase 4: Leaf Types
**Goal**: All non-composite field types parse correctly: strings, enums, bit fields, and padding
**Depends on**: Phase 3
**Requirements**: STRE-01, STRE-02, STRE-03, STRE-04, BITS-01, BITS-02, BITS-03, BITS-04, COMP-04
**Success Criteria** (what must be TRUE):
  1. Fixed-length ASCII and UTF-8 string fields are readable via parsed["fieldName"].GetString()
  2. Enum fields return the mapped string label via parsed["name"] and the raw numeric value via parsed["names"]
  3. Bit container fields extract sub-fields at specified bit positions, including boolean (1-bit) and multi-bit unsigned values
  4. Multi-byte (16-bit) bit containers respect endianness when reading the container value
  5. Padding fields advance the parse cursor without appearing in the ParseResult
**Plans**: TBD

Plans:
- [ ] 04-01: TBD

### Phase 5: Composite Types
**Goal**: Arrays and nested structs parse correctly with path-based access to elements
**Depends on**: Phase 4
**Requirements**: COMP-01, COMP-02, COMP-03, COMP-05
**Success Criteria** (what must be TRUE):
  1. Fixed arrays (count as literal number) parse N elements of the specified type
  2. Semi-dynamic arrays (count referencing another field) resolve the element count at parse time
  3. Struct elements inside arrays have scoped dependency chains with offsets relative to element start
  4. Path-based access works for nested elements: parsed["arrayName/0/fieldName"] returns the correct value
**Plans**: TBD

Plans:
- [ ] 05-01: TBD

### Phase 6: Validation
**Goal**: Parsed values are validated against contract-defined constraints with all errors collected
**Depends on**: Phase 5
**Requirements**: VALD-01, VALD-02, VALD-03, VALD-04, VALD-05
**Success Criteria** (what must be TRUE):
  1. Numeric fields outside min/max range produce validation errors with field path context
  2. String fields that violate pattern, minLength, or maxLength produce validation errors
  3. Multiple validation errors across different fields are collected (not fail-fast) and accessible on ParseResult
  4. Payload too short for the contract returns null rather than partial results with errors
**Plans**: TBD

Plans:
- [ ] 06-01: TBD

### Phase 7: Packaging
**Goal**: Gluey.Contract.Binary is published as a NuGet package with CI, tests, and documentation
**Depends on**: Phase 6
**Requirements**: PACK-01, PACK-02, PACK-03, PACK-04, PACK-05
**Success Criteria** (what must be TRUE):
  1. Gluey.Contract.Binary NuGet package builds and packs for net9.0 and net10.0
  2. CI pipeline runs build, test, and pack matching the Gluey.Contract.Json pipeline
  3. README contains usage examples covering contract loading, payload parsing, and value access
  4. Code coverage meets project standards with unit and integration tests across all field types
  5. Test project has InternalsVisibleTo access for white-box testing of internal components
**Plans**: TBD

Plans:
- [ ] 07-01: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Format Flag | 0/? | Not started | - |
| 2. Contract Model | 0/? | Not started | - |
| 3. Scalar Parsing | 0/? | Not started | - |
| 4. Leaf Types | 0/? | Not started | - |
| 5. Composite Types | 0/? | Not started | - |
| 6. Validation | 0/? | Not started | - |
| 7. Packaging | 0/? | Not started | - |
