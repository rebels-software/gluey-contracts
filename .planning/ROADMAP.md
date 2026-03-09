# Roadmap: Gluey.Contract

## Overview

Gluey.Contract delivers a zero-allocation, single-pass JSON Schema validator and indexer for .NET 9. The build progresses from foundational readonly struct types, through schema compilation and JSON byte reading, into validation keywords (split by complexity), culminating in the single-pass walker integration and quality proof. Each phase delivers a coherent, testable capability that the next phase builds upon.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Core Types** - Readonly struct foundations: ParsedProperty, OffsetTable, ValidationError, ErrorCollector, ParseResult, and dual API surface
- [ ] **Phase 2: Schema Model** - Schema loading, immutable SchemaNode tree with precomputed paths, and property index assignment
- [ ] **Phase 3: Schema References** - $ref/$defs resolution with cycle detection, $anchor support, and schema registry
- [ ] **Phase 4: JSON Byte Reader** - UTF-8 tokenizer with native byte offset tracking, multi-input support, and structural validation
- [ ] **Phase 5: Basic Validation** - Core keyword validators (type, enum, const, required, properties, items) and error collection pipeline
- [ ] **Phase 6: Constraint Validation** - Numeric, string, and size constraint validators
- [ ] **Phase 7: Composition and Conditionals** - allOf/anyOf/oneOf/not, if/then/else, and dependent keywords
- [ ] **Phase 8: Advanced Validation** - patternProperties, contains, uniqueItems, and format annotation/assertion
- [ ] **Phase 9: Single-Pass Walker** - SchemaWalker orchestrating reader + schema + offset table + error collector in one pass
- [ ] **Phase 10: Quality and Packaging** - BenchmarkDotNet zero-allocation proof, allocation regression tests, and NuGet packaging

## Phase Details

### Phase 1: Core Types
**Goal**: All foundational value types exist as readonly structs with correct shapes, enabling downstream phases to produce and consume them without layout changes
**Depends on**: Nothing (first phase)
**Requirements**: CORE-01, CORE-02, CORE-03, CORE-04, CORE-05, CORE-06, CORE-07
**Success Criteria** (what must be TRUE):
  1. ParsedProperty readonly struct can hold offset, length, and path into a byte buffer, and materialize values via GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal()
  2. OffsetTable maps property ordinals to byte positions using ArrayPool-backed storage and returns buffers on dispose
  3. ValidationError readonly struct carries an RFC 6901 JSON Pointer path, an error code enum value, and a static message string
  4. ErrorCollector pre-allocates a fixed-capacity buffer (default 64) and collects errors without heap allocation
  5. ParseResult readonly struct exposes success/failure state and parsed data access, implements IDisposable for buffer return, and the dual API compiles: TryParse returns bool with out parameter, Parse returns nullable and never throws
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md — Leaf types: ParsedProperty with value materialization, ValidationErrorCode enum, ValidationError struct, ValidationErrorMessages lookup
- [x] 01-02-PLAN.md — Container types: OffsetTable (ArrayPool-backed ordinal mapping) and ErrorCollector (pre-allocated error buffer with sentinel overflow)
- [x] 01-03-PLAN.md — Composite: ParseResult with dual indexers and IDisposable, dual API surface on JsonContractSchema (TryParse/Parse)

### Phase 2: Schema Model
**Goal**: JSON Schema documents can be loaded and compiled into an immutable, indexed schema tree with precomputed paths ready for validation
**Depends on**: Phase 1
**Requirements**: SCHM-01, SCHM-02, SCHM-05
**Success Criteria** (what must be TRUE):
  1. Schema can be loaded from raw JSON bytes and from a JSON string, producing an immutable SchemaNode tree
  2. Every node in the schema tree has a precomputed RFC 6901 JSON Pointer path (no string allocation needed during parse)
  3. Each property in the schema tree is assigned a stable integer index for offset table sizing
**Plans**: 2 plans

Plans:
- [ ] 02-01-PLAN.md — SchemaType flags enum + SchemaNode immutable class with all Draft 2020-12 keyword fields
- [ ] 02-02-PLAN.md — JsonSchemaLoader recursive-descent parser, SchemaIndexer ordinal assignment, TryLoad/Load API

### Phase 3: Schema References
**Goal**: Schemas with $ref, $defs, $anchor, and cross-schema references resolve correctly at load time, enabling composition of complex schemas
**Depends on**: Phase 2
**Requirements**: SCHM-03, SCHM-04, SCHM-06
**Success Criteria** (what must be TRUE):
  1. $ref and $defs references resolve at schema-load time, and circular references are detected and reported as errors
  2. $anchor declarations create named reference targets that $ref can resolve to
  3. A schema registry allows multiple schemas to be registered by URI, enabling cross-schema $ref resolution
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD

### Phase 4: JSON Byte Reader
**Goal**: Raw UTF-8 JSON bytes can be tokenized with native byte offset tracking, providing the foundation for zero-allocation validation
**Depends on**: Phase 1
**Requirements**: READ-01, READ-02, READ-03
**Success Criteria** (what must be TRUE):
  1. JSON byte tokenizer reads UTF-8 bytes and reports token type, byte offset, and byte length for each token
  2. Reader accepts byte[], ReadOnlySpan<byte>, and ReadOnlyMemory<byte> inputs through a unified API
  3. Structurally invalid JSON (mismatched braces, invalid tokens, truncated input) is detected and reported as errors
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD

### Phase 5: Basic Validation
**Goal**: Core JSON Schema keywords validate correctly against tokenized bytes, with all errors collected into the error pipeline
**Depends on**: Phase 2, Phase 4
**Requirements**: VALD-01, VALD-02, VALD-03, VALD-04, VALD-05, VALD-17
**Success Criteria** (what must be TRUE):
  1. type keyword validates all seven JSON Schema types: null, boolean, integer, number, string, array, object
  2. enum and const keywords compare values at the byte level without deserialization
  3. required keyword reports missing properties with correct JSON Pointer paths
  4. properties and additionalProperties keywords validate object structure, rejecting unknown properties when configured
  5. items and prefixItems keywords validate array elements against their schemas
  6. All validation errors are collected (not fail-fast), respecting a configurable maximum count (default 64)
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

### Phase 6: Constraint Validation
**Goal**: Numeric, string, and collection size constraints enforce value-level rules on validated JSON data
**Depends on**: Phase 5
**Requirements**: VALD-06, VALD-07, VALD-08
**Success Criteria** (what must be TRUE):
  1. minimum, maximum, exclusiveMinimum, exclusiveMaximum, and multipleOf enforce numeric constraints with correct precision
  2. minLength and maxLength count Unicode codepoints correctly, and pattern matches against string values
  3. minItems, maxItems, minProperties, and maxProperties enforce collection size constraints
**Plans**: TBD

Plans:
- [ ] 06-01: TBD
- [ ] 06-02: TBD

### Phase 7: Composition and Conditionals
**Goal**: Schema composition and conditional keywords enable complex validation logic without breaking single-pass semantics
**Depends on**: Phase 5
**Requirements**: VALD-09, VALD-10, VALD-11
**Success Criteria** (what must be TRUE):
  1. allOf requires all subschemas to pass; anyOf requires at least one; oneOf requires exactly one; not inverts the result
  2. if/then/else conditionally applies subschemas based on the result of the if-schema evaluation
  3. dependentRequired enforces property co-occurrence rules, and dependentSchemas applies subschemas when trigger properties are present
**Plans**: TBD

Plans:
- [ ] 07-01: TBD
- [ ] 07-02: TBD

### Phase 8: Advanced Validation
**Goal**: Remaining validation keywords complete JSON Schema Draft 2020-12 coverage for v1
**Depends on**: Phase 5
**Requirements**: VALD-12, VALD-13, VALD-14, VALD-15, VALD-16
**Success Criteria** (what must be TRUE):
  1. patternProperties matches property names against regex patterns and validates values; propertyNames validates all property name strings
  2. contains validates that at least one array element matches, with minContains and maxContains controlling the count
  3. uniqueItems detects duplicate array elements using a zero-allocation hashing strategy
  4. Format keywords are treated as annotations by default, with opt-in assertion mode; common formats (date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer) are implemented
**Plans**: TBD

Plans:
- [ ] 08-01: TBD
- [ ] 08-02: TBD

### Phase 9: Single-Pass Walker
**Goal**: Validation and offset table construction happen in a single forward pass through the byte buffer, delivering the library's core differentiator
**Depends on**: Phase 3, Phase 6, Phase 7, Phase 8
**Requirements**: INTG-01, INTG-02, INTG-03
**Success Criteria** (what must be TRUE):
  1. A single call validates JSON bytes against a schema and builds the offset table simultaneously -- no second pass
  2. Nested properties are accessible via offset table indexing (e.g., data["address"]["street"] resolves to the correct byte range)
  3. Array elements are accessible via offset table indexing (e.g., data["tags"][0] resolves to the correct byte range)
**Plans**: TBD

Plans:
- [ ] 09-01: TBD
- [ ] 09-02: TBD

### Phase 10: Quality and Packaging
**Goal**: Zero-allocation guarantees are proven by benchmarks and enforced by regression tests, and NuGet packages are ready to publish
**Depends on**: Phase 9
**Requirements**: QUAL-01, QUAL-02, QUAL-03
**Success Criteria** (what must be TRUE):
  1. BenchmarkDotNet suite runs end-to-end parse scenarios and reports zero heap allocations via MemoryDiagnoser
  2. Allocation regression tests using GC.GetAllocatedBytesForCurrentThread assert zero allocations and fail CI on regressions
  3. Gluey.Contract and Gluey.Contract.Json NuGet packages are configured with correct metadata, dependencies, and are packable
**Plans**: TBD

Plans:
- [ ] 10-01: TBD
- [ ] 10-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10
Note: Phases 4 and 2-3 are independent chains. Phases 6, 7, 8 depend on 5 but are independent of each other.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Core Types | 3/3 | Complete | 2026-03-08 |
| 2. Schema Model | 0/2 | Planning complete | - |
| 3. Schema References | 0/0 | Not started | - |
| 4. JSON Byte Reader | 0/0 | Not started | - |
| 5. Basic Validation | 0/0 | Not started | - |
| 6. Constraint Validation | 0/0 | Not started | - |
| 7. Composition and Conditionals | 0/0 | Not started | - |
| 8. Advanced Validation | 0/0 | Not started | - |
| 9. Single-Pass Walker | 0/0 | Not started | - |
| 10. Quality and Packaging | 0/0 | Not started | - |
