# Phase 9: Single-Pass Walker - Context

**Gathered:** 2026-03-10
**Status:** Ready for planning

<domain>
## Phase Boundary

SchemaWalker orchestrating JsonByteReader + SchemaNode tree + all validators (Phases 5-8) + OffsetTable + ErrorCollector in a single forward pass through JSON bytes. Implements JsonContractSchema.TryParse/Parse. Covers INTG-01 (single-pass validation + offset table), INTG-02 (nested property access), INTG-03 (array element access). Quality/benchmarks are Phase 10.

</domain>

<decisions>
## Implementation Decisions

### Walker Structure
- New `SchemaWalker` internal ref struct in Gluey.Contract.Json — separate from JsonContractSchema
- ref struct because it wraps JsonByteReader (also ref struct), lives on the stack, exists only for one Walk() call — zero heap allocation
- JsonContractSchema.TryParse/Parse delegate to SchemaWalker.Walk() static entry point
- Recursive traversal: WalkValue/WalkObject/WalkArray call each other recursively; schema node passed as parameter, stack frames track depth naturally

### Input Types
- byte[] primary: TryParse(byte[]) builds OffsetTable + validates (full parse with property access)
- ReadOnlySpan<byte> overload: validates only (no OffsetTable — can't store Span in ParsedProperty). ParseResult.IsValid works, indexers return Empty
- Both use the same walker core; a flag controls whether OffsetTable population is skipped

### Nested Property Indexing
- Hierarchical access: result["address"]["street"] chains through ParsedProperty sub-indexers
- ParsedProperty gains `this[string name]` and `this[int index]` indexers for object and array children
- Each ParsedProperty for an object/array holds a reference to child ordinal mappings enabling chained access
- Flat ordinals still assigned by SchemaIndexer, but access is explicitly hierarchical (not flat lookup)

### Array Element Storage
- Separate ArrayBuffer (ArrayPool-backed) for array elements — not in the OffsetTable
- OffsetTable remains fixed-size (schema-determined property count) for named properties
- ArrayBuffer tracks (array ordinal, element index) -> ParsedProperty mappings
- ParsedProperty for an array points to its ArrayBuffer region; result["tags"][0] resolves through it
- ArrayBuffer implements IDisposable; ParseResult cascades disposal to both OffsetTable and ArrayBuffer

### Validation Orchestration
- Type-first evaluation order per schema node:
  1. type (if fails, skip type-dependent keywords)
  2. enum / const
  3. Type-dependent constraints: numeric (min/max/multipleOf), string (minLength/maxLength/pattern/format), array (minItems/maxItems/uniqueItems/contains), object (required/minProperties/maxProperties)
  4. Composition: allOf/anyOf/oneOf/not
  5. Conditionals: if/then/else
  6. Dependencies: dependentRequired/dependentSchemas
- Composition keywords: walker reads value tokens once, captures state (seen properties, counts), then runs each subschema's validators against captured state — no re-reading bytes
- $ref: transparent follow — when walker encounters ResolvedRef, it validates against the resolved target node (pointer redirect, invisible to validation logic)

### Structural Error Handling
- Malformed JSON: walker stops immediately, converts structural error to ValidationError (with byte offset context), returns ParseResult with IsValid=false
- InvalidJson ValidationErrorCode added for structural errors

### Dual API Semantics
- TryParse: strict — returns true only when JSON is parseable AND schema-valid; returns false for any errors (structural or validation)
- Parse: rich — always returns a ParseResult with errors and data access (even on validation failure); returns null only for malformed JSON
- This means: TryParse is for "is it valid?" checks; Parse is for "give me everything" including error inspection and partial data access

### Claude's Discretion
- Internal method signatures for WalkValue/WalkObject/WalkArray
- ArrayBuffer internal design (initial capacity, growth strategy)
- How captured state for composition validation is structured
- HashSet<string> for seenProperties tracking (reuse vs per-object allocation)
- Exact integration of format validation (AssertFormat flag plumbing)

</decisions>

<specifics>
## Specific Ideas

- Walker should feel like a natural orchestrator — it reads tokens and dispatches to existing validators, not reimplementing validation logic
- The hierarchical access (result["address"]["street"]) is the explicitly preferred pattern over flat lookup

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `JsonByteReader`: ref struct, forward-only Read() loop with TokenType/ByteOffset/ByteLength — walker wraps this
- `KeywordValidator`: static ValidateType/Enum/Const/Required/AdditionalProperty/GetItemSchema methods
- `NumericValidator`: static Validate for min/max/exclusiveMin/exclusiveMax/multipleOf
- `StringValidator`: static Validate for minLength/maxLength/pattern
- `ArrayValidator`: static ValidateMinItems/MaxItems/UniqueItems/Contains
- `ObjectValidator`: static ValidateMinProperties/MaxProperties
- `CompositionValidator`: static ValidateAllOf/AnyOf/OneOf/Not with pre-computed pass counts
- `ConditionalValidator`: static ValidateIfThenElse
- `DependencyValidator`: static ValidateDependentRequired/DependentSchema
- `FormatValidator`: static Validate for 9 format types
- `OffsetTable`: ArrayPool-backed, Set(ordinal, property), indexed by ordinal
- `ErrorCollector`: ArrayPool-backed, Add(error), sentinel overflow at capacity
- `ParseResult`: wraps OffsetTable + ErrorCollector + nameToOrdinal dictionary
- `ParsedProperty`: readonly struct with byte[] + offset + length + path, value materialization methods
- `SchemaNode.BuildChildPath`: RFC 6901 path construction helper

### Established Patterns
- All validators are static methods receiving SchemaNode + ErrorCollector — walker calls them
- Validators return bool (pass/fail) enabling short-circuiting
- ErrorCollector.Add handles overflow internally — validators never check IsFull
- SchemaNode.Path provides precomputed RFC 6901 paths
- Internal visibility for implementation types (reader, walker, validators)

### Integration Points
- JsonContractSchema.TryParse/Parse: currently stubs returning false/null — walker replaces these
- JsonContractSchema holds _root (SchemaNode), _nameToOrdinal (Dictionary), PropertyCount, AssertFormat
- ParseResult constructor is internal — walker creates it directly
- OffsetTable constructor is internal — walker creates it with schema's PropertyCount
- ParsedProperty constructor is internal — walker creates them from byte[] + reader offsets + SchemaNode.Path

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 09-single-pass-walker*
*Context gathered: 2026-03-10*
