# Phase 8: Advanced Validation - Context

**Gathered:** 2026-03-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Remaining validation keywords to complete JSON Schema Draft 2020-12 coverage for v1: patternProperties, propertyNames, contains/minContains/maxContains, uniqueItems, and format annotation/assertion. Each keyword follows the established static validator class pattern.

</domain>

<decisions>
## Implementation Decisions

### Format Assertion API
- Opt-in via `SchemaOptions` sealed class passed to `TryLoad`/`Load` — e.g., `TryLoad(json, new SchemaOptions { AssertFormat = true })`
- `SchemaOptions` is a public sealed class with sensible defaults
- All-or-nothing: `AssertFormat = true` enables assertion for ALL recognized formats
- Unrecognized format strings pass silently (spec-compliant: implementations SHOULD NOT fail on unknown formats)

### Format Coverage
- Implement all 9 formats for v1: date-time, date, time, email, uuid, uri, ipv4, ipv6, json-pointer
- Email validation: simplified structural check (has @, non-empty local/domain, valid characters) — not full RFC 5321
- Date/time validation: use `DateTimeOffset.TryParse` (leverages .NET built-in parser)
- Organized as a single static `FormatValidator` class with dispatcher and private per-format methods — consistent with existing validator pattern

### Allocation Budget for Format
- Accept allocations in format assertion path — format is opt-in, allocations only when `AssertFormat=true` AND format keyword present
- Zero-alloc guarantee applies to core validation path; format assertion is documented exception
- Document allocation behavior in BOTH XML doc comment on `AssertFormat` property AND project docs
- Optimize where easy: use stackalloc'd string conversion, `Guid.TryParse`, `IPAddress.TryParse` where .NET APIs make zero-alloc practical

### uniqueItems Strategy
- Hybrid approach: FNV-1a hash of raw bytes into stackalloc'd hash set for arrays <= 128 items; O(n^2) byte comparison fallback for collisions
- Stack threshold: 128 items (128 x sizeof(int) = 512 bytes on stack)
- Spec-compliant numeric equivalence: 1 and 1.0 are duplicates — byte-level first, then numeric decimal fallback (same pattern as enum/const in KeywordValidator)

### Claude's Discretion
- patternProperties and propertyNames implementation details (well-defined by spec)
- contains/minContains/maxContains implementation approach
- Hash function choice and collision handling details for uniqueItems
- Internal method signatures and parameter ordering

</decisions>

<specifics>
## Specific Ideas

No specific requirements — implementations follow JSON Schema Draft 2020-12 spec exactly. The validator pattern established in Phases 5-7 (static classes, bool return, ErrorCollector) continues.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SchemaNode` already has all Phase 8 keyword fields: PatternProperties, PropertyNames, Contains, MinContains, MaxContains, UniqueItems, Format
- `ValidationErrorCode` already has all Phase 8 codes: PatternPropertyInvalid, PropertyNameInvalid, ContainsInvalid, MinContainsExceeded, MaxContainsExceeded, UniqueItemsViolation, FormatInvalid
- `ValidationErrorMessages` lookup ready for new codes
- `StringValidator.ValidatePattern` and `SchemaNode.CompiledPattern` — patternProperties regex matching follows same compiled-regex pattern
- `KeywordValidator.TryNumericEqual` — reusable for uniqueItems numeric equivalence
- `JsonSchemaLoader` already parses all Phase 8 keywords (patternProperties, propertyNames, contains, uniqueItems, format)

### Established Patterns
- Static validator classes (KeywordValidator, NumericValidator, StringValidator, ArrayValidator, ObjectValidator, CompositionValidator, ConditionalValidator, DependencyValidator)
- Methods return bool, push errors to ErrorCollector
- Regex compiled at schema load time with RegexOptions.Compiled (Phase 6 decision)

### Integration Points
- New validator classes integrate with future SchemaWalker (Phase 9)
- `SchemaOptions` will be a new public type on `JsonContractSchema.TryLoad`/`Load`
- `AssertFormat` flag needs to propagate from load time to validation time (stored on schema or passed through)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 08-advanced-validation*
*Context gathered: 2026-03-10*
