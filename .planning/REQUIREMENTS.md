# Requirements: Gluey.Contract

**Defined:** 2026-03-08
**Core Value:** Zero-allocation, single-pass validation and indexing of raw bytes against a schema

## v1 Requirements

### Core Types

- [x] **CORE-01**: ParsedProperty readonly struct with offset, length, and path into byte buffer
- [x] **CORE-02**: On-demand value materialization via GetString(), GetInt32(), GetInt64(), GetDouble(), GetBoolean(), GetDecimal()
- [x] **CORE-03**: Offset table mapping schema property ordinals to byte positions (ArrayPool-backed)
- [x] **CORE-04**: ValidationError readonly struct with RFC 6901 path, error code enum, and static message
- [x] **CORE-05**: ErrorCollector with pre-allocated buffer, max 64 errors default
- [x] **CORE-06**: ParseResult readonly struct with success/failure and parsed data access, IDisposable for buffer return
- [x] **CORE-07**: Dual API surface: TryParse (bool + out) and Parse (returns nullable, never throws)

### Schema

- [x] **SCHM-01**: Schema loading from JSON bytes and JSON string
- [x] **SCHM-02**: SchemaNode immutable tree with precomputed JSON Pointer paths
- [x] **SCHM-03**: $ref / $defs resolution at schema-load time with cycle detection
- [x] **SCHM-04**: $anchor resolution for named reference targets
- [x] **SCHM-05**: Property index assignment for zero-allocation offset table sizing
- [x] **SCHM-06**: Schema registry for multi-schema $ref resolution by URI

### Reader

- [x] **READ-01**: JSON byte tokenizer with native byte offset tracking
- [x] **READ-02**: Accept byte[], ReadOnlySpan<byte>, and ReadOnlyMemory<byte> inputs
- [x] **READ-03**: Structural JSON validation (well-formedness)

### Validation

- [x] **VALD-01**: type keyword (null, boolean, integer, number, string, array, object)
- [x] **VALD-02**: enum and const keywords with byte-level comparison
- [x] **VALD-03**: required keyword
- [x] **VALD-04**: properties and additionalProperties keywords
- [x] **VALD-05**: items and prefixItems keywords
- [x] **VALD-06**: minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf
- [x] **VALD-07**: minLength, maxLength (Unicode codepoint counting), pattern
- [ ] **VALD-08**: minItems, maxItems, minProperties, maxProperties
- [ ] **VALD-09**: allOf, anyOf, oneOf, not composition
- [ ] **VALD-10**: if / then / else conditional validation
- [ ] **VALD-11**: dependentRequired and dependentSchemas
- [ ] **VALD-12**: patternProperties and propertyNames
- [ ] **VALD-13**: contains, minContains, maxContains
- [ ] **VALD-14**: uniqueItems with zero-allocation hashing strategy
- [ ] **VALD-15**: Format annotation by default, opt-in format assertion
- [ ] **VALD-16**: Common format validators: date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer
- [x] **VALD-17**: All errors collected per parse (not fail-fast), configurable max count

### Integration

- [ ] **INTG-01**: Single-pass validation + offset table construction
- [ ] **INTG-02**: Nested property access via offset table (data["address"]["street"])
- [ ] **INTG-03**: Array element access via offset table (data["tags"][0])

### Quality

- [ ] **QUAL-01**: BenchmarkDotNet suite proving zero heap allocation on parse path
- [ ] **QUAL-02**: Allocation regression tests using GC.GetAllocatedBytesForCurrentThread
- [ ] **QUAL-03**: NuGet packages configured and ready for publishing

## v2 Requirements

### Unevaluated Keywords

- **UEVL-01**: unevaluatedProperties with annotation tracking across applicators
- **UEVL-02**: unevaluatedItems with annotation tracking across applicators

### Dynamic References

- **DYNR-01**: $dynamicRef runtime resolution
- **DYNR-02**: $dynamicAnchor target declaration

### Extensions

- **EXTN-01**: Custom keyword extension API
- **EXTN-02**: JSON Schema output format (List/Hierarchical per spec)
- **EXTN-03**: Content vocabulary (contentMediaType, contentEncoding, contentSchema)
- **EXTN-04**: Meta-data annotations collection API (title, description, default, deprecated)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Full deserialization to POCO/objects | Defeats zero-allocation purpose; System.Text.Json already does this |
| Schema generation from C# types | Huge surface area, already solved by NJsonSchema |
| Code generation from schema | Different product; Corvus.JsonSchema does this |
| Stream/async input processing | Breaks single-pass contiguous buffer model |
| $vocabulary meta-schema validation | Schema-authoring concern, not runtime validation |
| Remote $ref resolution (HTTP) | Security concerns (SSRF), violates no-dependencies constraint |
| Business/domain validation rules | Application-layer concern |
| Gluey.Contract.AspNetCore | Separate package, future milestone |
| Gluey.Contract.Protobuf | Separate format driver, future milestone |
| Gluey.Contract.Postgres | Separate format driver, future milestone |
| Gluey.Contract.Redis | Separate format driver, future milestone |
| Result<T> pattern | Parse returns nullable instead; simpler API |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | Phase 1: Core Types | Complete |
| CORE-02 | Phase 1: Core Types | Complete |
| CORE-03 | Phase 1: Core Types | Complete |
| CORE-04 | Phase 1: Core Types | Complete |
| CORE-05 | Phase 1: Core Types | Complete |
| CORE-06 | Phase 1: Core Types | Complete |
| CORE-07 | Phase 1: Core Types | Complete |
| SCHM-01 | Phase 2: Schema Model | Complete |
| SCHM-02 | Phase 2: Schema Model | Complete |
| SCHM-05 | Phase 2: Schema Model | Complete |
| SCHM-03 | Phase 3: Schema References | Complete |
| SCHM-04 | Phase 3: Schema References | Complete |
| SCHM-06 | Phase 3: Schema References | Complete |
| READ-01 | Phase 4: JSON Byte Reader | Complete |
| READ-02 | Phase 4: JSON Byte Reader | Complete |
| READ-03 | Phase 4: JSON Byte Reader | Complete |
| VALD-01 | Phase 5: Basic Validation | Complete |
| VALD-02 | Phase 5: Basic Validation | Complete |
| VALD-03 | Phase 5: Basic Validation | Complete |
| VALD-04 | Phase 5: Basic Validation | Complete |
| VALD-05 | Phase 5: Basic Validation | Complete |
| VALD-17 | Phase 5: Basic Validation | Complete |
| VALD-06 | Phase 6: Constraint Validation | Complete |
| VALD-07 | Phase 6: Constraint Validation | Complete |
| VALD-08 | Phase 6: Constraint Validation | Pending |
| VALD-09 | Phase 7: Composition and Conditionals | Pending |
| VALD-10 | Phase 7: Composition and Conditionals | Pending |
| VALD-11 | Phase 7: Composition and Conditionals | Pending |
| VALD-12 | Phase 8: Advanced Validation | Pending |
| VALD-13 | Phase 8: Advanced Validation | Pending |
| VALD-14 | Phase 8: Advanced Validation | Pending |
| VALD-15 | Phase 8: Advanced Validation | Pending |
| VALD-16 | Phase 8: Advanced Validation | Pending |
| INTG-01 | Phase 9: Single-Pass Walker | Pending |
| INTG-02 | Phase 9: Single-Pass Walker | Pending |
| INTG-03 | Phase 9: Single-Pass Walker | Pending |
| QUAL-01 | Phase 10: Quality and Packaging | Pending |
| QUAL-02 | Phase 10: Quality and Packaging | Pending |
| QUAL-03 | Phase 10: Quality and Packaging | Pending |

**Coverage:**
- v1 requirements: 33 total
- Mapped to phases: 33
- Unmapped: 0

---
*Requirements defined: 2026-03-08*
*Last updated: 2026-03-08 after roadmap creation*
