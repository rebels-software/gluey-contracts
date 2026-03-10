# Phase 9: Single-Pass Walker - Research

**Researched:** 2026-03-10
**Domain:** JSON Schema validation walker / single-pass parse orchestration
**Confidence:** HIGH

## Summary

Phase 9 is the integration phase that connects all previously-built components into a working system. The SchemaWalker is a new `internal ref struct` in Gluey.Contract.Json that wraps JsonByteReader and walks the token stream against a SchemaNode tree, dispatching to existing validators, populating an OffsetTable for named properties, and building a separate ArrayBuffer for array element access. All code for this phase is new orchestration logic -- no new algorithmic complexity beyond what exists in the validators.

The key technical challenges are: (1) designing the ArrayBuffer for dynamic array element storage alongside the fixed-size OffsetTable, (2) implementing hierarchical ParsedProperty indexers (result["address"]["street"]) that chain through sub-objects, (3) handling composition/conditional keywords by capturing validation state without re-reading tokens, and (4) ref struct constraints limiting what the walker can do (no async, no boxing, no interface implementation).

**Primary recommendation:** Split into two plans: Plan 1 builds SchemaWalker core + OffsetTable population + TryParse/Parse wiring (INTG-01), Plan 2 adds ArrayBuffer + hierarchical ParsedProperty indexers for nested/array access (INTG-02, INTG-03).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- New `SchemaWalker` internal ref struct in Gluey.Contract.Json -- separate from JsonContractSchema
- ref struct because it wraps JsonByteReader (also ref struct), lives on the stack, exists only for one Walk() call -- zero heap allocation
- JsonContractSchema.TryParse/Parse delegate to SchemaWalker.Walk() static entry point
- Recursive traversal: WalkValue/WalkObject/WalkArray call each other recursively; schema node passed as parameter, stack frames track depth naturally
- byte[] primary: TryParse(byte[]) builds OffsetTable + validates (full parse with property access)
- ReadOnlySpan<byte> overload: validates only (no OffsetTable -- can't store Span in ParsedProperty). ParseResult.IsValid works, indexers return Empty
- Both use the same walker core; a flag controls whether OffsetTable population is skipped
- Hierarchical access: result["address"]["street"] chains through ParsedProperty sub-indexers
- ParsedProperty gains `this[string name]` and `this[int index]` indexers for object and array children
- Each ParsedProperty for an object/array holds a reference to child ordinal mappings enabling chained access
- Flat ordinals still assigned by SchemaIndexer, but access is explicitly hierarchical (not flat lookup)
- Separate ArrayBuffer (ArrayPool-backed) for array elements -- not in the OffsetTable
- OffsetTable remains fixed-size (schema-determined property count) for named properties
- ArrayBuffer tracks (array ordinal, element index) -> ParsedProperty mappings
- ParsedProperty for an array points to its ArrayBuffer region; result["tags"][0] resolves through it
- ArrayBuffer implements IDisposable; ParseResult cascades disposal to both OffsetTable and ArrayBuffer
- Type-first evaluation order per schema node (see CONTEXT.md for full ordering)
- Composition keywords: walker reads value tokens once, captures state (seen properties, counts), then runs each subschema's validators against captured state -- no re-reading bytes
- $ref: transparent follow -- when walker encounters ResolvedRef, it validates against the resolved target node
- Malformed JSON: walker stops immediately, converts structural error to ValidationError (with byte offset context), returns ParseResult with IsValid=false
- InvalidJson ValidationErrorCode added for structural errors
- TryParse: strict -- returns true only when JSON is parseable AND schema-valid
- Parse: rich -- always returns a ParseResult with errors and data access (even on validation failure); returns null only for malformed JSON

### Claude's Discretion
- Internal method signatures for WalkValue/WalkObject/WalkArray
- ArrayBuffer internal design (initial capacity, growth strategy)
- How captured state for composition validation is structured
- HashSet<string> for seenProperties tracking (reuse vs per-object allocation)
- Exact integration of format validation (AssertFormat flag plumbing)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| INTG-01 | Single-pass validation + offset table construction | SchemaWalker.Walk() orchestrates JsonByteReader + all validators + OffsetTable in one forward pass. TryParse/Parse replace stubs. |
| INTG-02 | Nested property access via offset table (data["address"]["street"]) | ParsedProperty gains string/int indexers + child mapping references. Hierarchical chaining through sub-objects. |
| INTG-03 | Array element access via offset table (data["tags"][0]) | ArrayBuffer (ArrayPool-backed) stores array element ParsedProperty values. ParsedProperty int indexer resolves through ArrayBuffer region. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9.0 | net9.0 | Target framework | Already established in project |
| System.Text.Json | built-in | Utf8JsonReader backing JsonByteReader | Already used for tokenization |
| System.Buffers | built-in | ArrayPool for OffsetTable, ErrorCollector, ArrayBuffer | Zero-allocation pattern |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | All integration tests |
| FluentAssertions | 8.0.1 | Test assertions | All test assertions |

### Alternatives Considered
None -- this phase uses only existing project dependencies.

**Installation:**
No new packages needed.

## Architecture Patterns

### New Files

```
src/Gluey.Contract.Json/SchemaWalker.cs       # ref struct walker (new)
src/Gluey.Contract/ArrayBuffer.cs              # ArrayPool-backed array element storage (new)
```

### Modified Files

```
src/Gluey.Contract/ParsedProperty.cs           # Add string/int indexers + child references
src/Gluey.Contract/ParseResult.cs              # Add ArrayBuffer disposal, possibly constructor overload
src/Gluey.Contract/ValidationErrorCode.cs      # Add InvalidJson error code
src/Gluey.Contract/ValidationErrorMessages.cs  # Add InvalidJson message
src/Gluey.Contract.Json/JsonContractSchema.cs  # Replace TryParse/Parse stubs with walker calls
```

### Pattern 1: SchemaWalker as Orchestrator

**What:** SchemaWalker is a ref struct with static entry point Walk() that creates a walker instance, runs the forward pass, and returns the results. It calls existing static validator methods -- never reimplements validation logic.

**When to use:** This is the single entry pattern for all parse operations.

**Design:**
```csharp
internal ref struct SchemaWalker
{
    private JsonByteReader _reader;
    private readonly SchemaNode _root;
    private readonly byte[] _data;           // null for Span overload
    private readonly ErrorCollector _errors;
    private readonly OffsetTable _table;
    private readonly ArrayBuffer _arrayBuffer;
    private readonly Dictionary<string, int> _nameToOrdinal;
    private readonly bool _buildOffsets;      // false for Span overload
    private readonly bool _assertFormat;

    internal static WalkResult Walk(
        byte[] data,
        SchemaNode root,
        Dictionary<string, int> nameToOrdinal,
        int propertyCount,
        bool assertFormat)
    {
        // Creates reader, table, errors, arrayBuffer
        // Calls WalkValue on root
        // Returns WalkResult with table, errors, arrayBuffer
    }

    private bool WalkValue(SchemaNode node)
    {
        // Follow $ref if present
        // Read next token
        // Handle boolean schemas
        // Validate type keyword first
        // Dispatch to WalkObject/WalkArray for containers
        // Validate scalar constraints (enum, const, numeric, string, format)
        // Validate composition (allOf/anyOf/oneOf/not)
        // Validate conditionals (if/then/else)
        // Store in OffsetTable if named property
        // Return true/false
    }

    private bool WalkObject(SchemaNode node, string path)
    {
        // Track seen properties (HashSet<string>)
        // Track property count
        // For each PropertyName token:
        //   - Look up child schema in node.Properties
        //   - Check additionalProperties / patternProperties
        //   - Recursively WalkValue on child schema
        //   - Record in OffsetTable if ordinal exists
        // After EndObject:
        //   - ValidateRequired
        //   - ValidateMinProperties/MaxProperties
        //   - ValidateDependentRequired/DependentSchemas
        //   - ValidatePropertyNames
    }

    private bool WalkArray(SchemaNode node, string path)
    {
        // Track element count, contains match count
        // Collect element bytes for uniqueItems
        // For each element:
        //   - GetItemSchema for the element index
        //   - Recursively WalkValue
        //   - Store in ArrayBuffer
        // After EndArray:
        //   - ValidateMinItems/MaxItems
        //   - ValidateContains
        //   - ValidateUniqueItems
    }
}
```

### Pattern 2: ArrayBuffer Design

**What:** ArrayPool-backed growable storage for array element ParsedProperty values, keyed by (array ordinal, element index).

**Design considerations (Claude's discretion):**
- Initial capacity: 16 elements (reasonable default for typical JSON arrays)
- Growth strategy: double capacity on overflow (standard ArrayPool pattern)
- Storage: flat ParsedProperty[] with a parallel tracking structure mapping array ordinal to (startIndex, count) ranges
- This avoids nested allocation -- a single contiguous array with offset tracking

```csharp
internal struct ArrayBuffer : IDisposable
{
    private ParsedProperty[]? _entries;
    private int _count;
    private int _capacity;
    // Maps array ordinal -> (startIndex, count) in _entries
    private Dictionary<int, (int Start, int Count)>? _regions;

    internal ArrayBuffer(int initialCapacity = 16) { ... }
    internal void Add(int arrayOrdinal, ParsedProperty element) { ... }
    internal ParsedProperty Get(int arrayOrdinal, int elementIndex) { ... }
    internal (int Start, int Count) GetRegion(int arrayOrdinal) { ... }
    public void Dispose() { ... }
}
```

### Pattern 3: ParsedProperty Hierarchical Indexers

**What:** ParsedProperty gains sub-indexers for chained access. An object-typed ParsedProperty can look up child properties by name; an array-typed one can look up elements by index.

**Key insight:** ParsedProperty is currently a simple offset+length struct. To support indexers, it needs references to child data. Two approaches:

**Recommended approach:** Add optional fields to ParsedProperty for child resolution:
- `_childOrdinals`: Dictionary<string, int>? for object children (maps child name -> ordinal in OffsetTable)
- `_arrayBuffer`: ArrayBuffer reference for array children
- `_arrayOrdinal`: int for which array region this property maps to
- `_offsetTable`: OffsetTable reference for resolving child ordinals

Since ParsedProperty holds `byte[] _buffer` (reference type), adding more reference fields does not violate the readonly struct contract. The struct gets larger but remains stack-friendly.

```csharp
public readonly struct ParsedProperty
{
    // Existing fields
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;

    // New fields for hierarchical access
    private readonly OffsetTable _childTable;           // For resolving child ordinals
    private readonly Dictionary<string, int>? _childOrdinals; // name -> ordinal for object children
    private readonly ArrayBuffer _arrayBuffer;          // For array element access
    private readonly int _arrayOrdinal;                 // Which array region (-1 if not array)

    public ParsedProperty this[string name] { get { ... } }
    public ParsedProperty this[int index] { get { ... } }
}
```

**Important:** This means ParsedProperty needs new internal constructors that accept the child-resolution references. The existing constructor remains for backward compatibility. The walker creates ParsedProperty instances with the full set of fields.

### Pattern 4: Composition Validation Without Re-reading

**What:** For allOf/anyOf/oneOf/not, the walker cannot re-read tokens. Instead, it captures the current value's state and runs each subschema's validators against that state.

**For scalar values:** The walker has the token bytes, token type, and isInteger flag. It can run each subschema's type/enum/const/numeric/string validators using the already-read token data.

**For container values (objects/arrays):** The walker has already walked the children. It tracks:
- seenProperties (HashSet<string>) for the object
- propertyCount (int)
- elementCount (int)
- element bytes for uniqueItems

Each subschema's object/array validators (required, minProperties, etc.) can be run against this captured state. The tricky part is that subschemas may define their own `properties` with different constraints -- the walker needs to check the subschema's property schemas against the already-walked property values.

**Practical simplification:** For v1, composition on container values validates structural constraints (required, min/max properties/items) using captured state. Property-level validation within composition subschemas operates on the already-populated OffsetTable data. This is a known simplification that covers the vast majority of real-world schemas.

### Anti-Patterns to Avoid
- **Re-reading tokens for composition:** Never seek backward or re-tokenize. Capture state once.
- **Allocating per-property in hot path:** Use pre-allocated structures (OffsetTable, ArrayBuffer, ErrorCollector).
- **Walker doing validation logic:** Walker dispatches to static validator methods. Never duplicate validation code.
- **Storing Span<byte> in ParsedProperty:** Can't store Span in a class/struct field. Use byte[] + offset + length (already the pattern).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON tokenization | Custom tokenizer | JsonByteReader (wrapping Utf8JsonReader) | Battle-tested, handles all edge cases |
| Type validation | Type checking logic | KeywordValidator.ValidateType | Already handles flags enum, integer subset |
| Numeric parsing | Custom number parsing | NumericValidator.TryParseDecimal | Uses Utf8JsonReader internally |
| Error collection | Manual list/array | ErrorCollector | ArrayPool-backed, sentinel overflow |
| Property storage | Manual array | OffsetTable | ArrayPool-backed, ordinal-indexed |
| Pool management | Manual ArrayPool.Rent/Return | Existing Dispose patterns | Already established in OffsetTable/ErrorCollector |

**Key insight:** This phase is almost entirely orchestration. Every validation primitive already exists. The walker's job is to read tokens, determine which validators to call, pass the right arguments, and store results.

## Common Pitfalls

### Pitfall 1: ref struct Limitations
**What goes wrong:** ref struct cannot implement interfaces, be boxed, or be captured in lambdas/async methods.
**Why it happens:** SchemaWalker is ref struct because it wraps JsonByteReader (also ref struct).
**How to avoid:** Walk() is a static method that creates, uses, and discards the walker. Return a plain struct/class WalkResult, not the ref struct itself. Never try to store the walker in a field.
**Warning signs:** Compiler error CS8345 (ref struct cannot implement interface), CS8350 (cannot use ref struct in this context).

### Pitfall 2: ParsedProperty Size Growth
**What goes wrong:** Adding child-resolution fields to ParsedProperty increases struct size significantly, potentially causing copy overhead.
**Why it happens:** ParsedProperty is a readonly struct passed by value. Each new reference field adds 8 bytes (on 64-bit).
**How to avoid:** Current: 4 fields (byte[], int, int, string) = ~24 bytes. With children: add ~32 bytes (OffsetTable, Dictionary, ArrayBuffer, int). Total ~56 bytes. This is within acceptable limits for a by-value struct. If it becomes a concern, the child-resolution data could be pulled into a separate ChildResolver class referenced by a single field, but this adds indirection.
**Warning signs:** Profiling shows excessive copying in tight loops.

### Pitfall 3: ArrayBuffer Ordinal Tracking
**What goes wrong:** Array elements don't have schema-assigned ordinals (SchemaIndexer skips array items). The walker needs to create a mapping from the parent array's ordinal to its elements.
**Why it happens:** SchemaIndexer assigns ordinals only to named properties in Properties dictionaries.
**How to avoid:** The ArrayBuffer uses the parent array property's ordinal as the key, not individual element ordinals. The walker knows the parent ordinal from the OffsetTable lookup.
**Warning signs:** Trying to assign ordinals to array elements, or expecting array elements in the OffsetTable.

### Pitfall 4: Composition Validation State Capture
**What goes wrong:** Composition subschemas may reference different property schemas than the parent, leading to incomplete validation.
**Why it happens:** allOf/anyOf/oneOf can each define their own properties constraints.
**How to avoid:** For scalar values, re-run each subschema's validators with the same token data. For containers, use a temporary ErrorCollector per subschema to count pass/fail without polluting the main collector, then report composition-level errors.
**Warning signs:** Composition tests passing when they shouldn't, or extra errors appearing from subschema evaluation.

### Pitfall 5: OffsetTable vs ArrayBuffer Reference in ParsedProperty
**What goes wrong:** ParsedProperty currently takes byte[], int, int, string. Adding OffsetTable/ArrayBuffer references means the struct holds references to IDisposable resources it doesn't own.
**Why it happens:** ParseResult owns disposal. ParsedProperty just borrows references for indexing.
**How to avoid:** Document clearly that ParsedProperty is only valid within the lifetime of its parent ParseResult. After Dispose(), indexers return Empty (defensive null checks on internal references).
**Warning signs:** ObjectDisposedException or accessing returned-to-pool buffers.

### Pitfall 6: Malformed JSON Error Conversion
**What goes wrong:** JsonByteReader reports structural errors as JsonReadError with byte offset but no JSON Pointer path.
**Why it happens:** Structural errors occur before/during schema-level processing, so no schema path context exists.
**How to avoid:** Use the root path ("") or the current node's path for the ValidationError. Include the byte offset in context. Add InvalidJson to ValidationErrorCode enum.
**Warning signs:** Null reference on error path, or empty errors with no useful location info.

### Pitfall 7: Dual API Semantics (TryParse vs Parse)
**What goes wrong:** Inconsistent behavior between TryParse and Parse for edge cases.
**Why it happens:** Different return contracts: TryParse returns bool+out, Parse returns nullable ParseResult.
**How to avoid:** Both call the same Walk() method. Difference is only in how the result is packaged:
- TryParse: Walk() -> if no structural errors AND no validation errors -> true + result; else false
- Parse: Walk() -> if structural error (malformed JSON) -> null; else -> ParseResult (even with validation errors)
**Warning signs:** Tests showing Parse returns null for valid-but-schema-invalid JSON, or TryParse returns true with errors.

## Code Examples

### Walker Entry Point Pattern
```csharp
// In JsonContractSchema (replacing stubs):
public bool TryParse(byte[] data, out ParseResult result)
{
    var walkResult = SchemaWalker.Walk(data, _root, _nameToOrdinal, PropertyCount, AssertFormat);

    if (walkResult.HasStructuralError || walkResult.Errors.HasErrors)
    {
        result = walkResult.HasStructuralError
            ? default
            : new ParseResult(walkResult.Table, walkResult.Errors, _nameToOrdinal, walkResult.ArrayBuffer);
        if (walkResult.HasStructuralError)
        {
            walkResult.Table.Dispose();
            walkResult.Errors.Dispose();
            walkResult.ArrayBuffer.Dispose();
        }
        return false;
    }

    result = new ParseResult(walkResult.Table, walkResult.Errors, _nameToOrdinal, walkResult.ArrayBuffer);
    return true;
}

public ParseResult? Parse(byte[] data)
{
    var walkResult = SchemaWalker.Walk(data, _root, _nameToOrdinal, PropertyCount, AssertFormat);

    if (walkResult.HasStructuralError)
    {
        walkResult.Table.Dispose();
        walkResult.Errors.Dispose();
        walkResult.ArrayBuffer.Dispose();
        return null;
    }

    return new ParseResult(walkResult.Table, walkResult.Errors, _nameToOrdinal, walkResult.ArrayBuffer);
}
```

### Validator Dispatch Pattern (already established)
```csharp
// Walker calls validators -- it does NOT validate itself:
if (node.Type.HasValue)
{
    if (!KeywordValidator.ValidateType(node.Type.Value, tokenType, isInteger, node.Path, _errors))
    {
        typeValid = false;
        // Skip type-dependent constraints
    }
}

if (typeValid && node.Minimum.HasValue)
{
    if (NumericValidator.TryParseDecimal(tokenBytes, out decimal value))
    {
        NumericValidator.ValidateMinimum(value, node.Minimum.Value, node.Path, _errors);
    }
}
```

### $ref Transparent Follow Pattern
```csharp
private bool WalkValue(SchemaNode node)
{
    // Transparent $ref follow
    var effective = node.ResolvedRef ?? node;

    // Boolean schema short-circuit
    if (effective.BooleanSchema.HasValue)
    {
        if (!effective.BooleanSchema.Value)
        {
            // false schema rejects everything -- but still need to consume tokens
            SkipValue();
            _errors.Add(new ValidationError(effective.Path, ...));
            return false;
        }
        SkipValue(); // true schema accepts everything
        return true;
    }

    // Read next token and validate...
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Two-pass (parse then validate) | Single-pass (validate while parsing) | This phase | Core library differentiator |
| Flat property lookup | Hierarchical chaining | This phase | result["a"]["b"] instead of result["/a/b"] |
| Array elements in OffsetTable | Separate ArrayBuffer | This phase | OffsetTable stays fixed-size |

## Open Questions

1. **ParsedProperty struct size after adding child indexers**
   - What we know: Adding 4 reference/value fields (~32 bytes) to a ~24 byte struct
   - What's unclear: Whether 56-byte struct causes measurable copy overhead in hot paths
   - Recommendation: Implement directly, benchmark in Phase 10. If needed, extract child data into a single ChildContext class

2. **Composition validation on nested objects**
   - What we know: Scalar composition is straightforward (same token, multiple validators). Container composition needs captured state.
   - What's unclear: Whether allOf subschemas with different `properties` constraints need full re-walk of children
   - Recommendation: For v1, composition subschemas validate structural constraints (required, sizes) against captured state. Per-property type constraints within composition subschemas are a known gap to address if test cases demand it.

3. **ReadOnlySpan<byte> overload for TryParse**
   - What we know: Cannot store Span in ParsedProperty. Flag controls offset table population.
   - What's unclear: Whether to keep the same return type (ParseResult with empty indexers) or use a different ValidationResult type
   - Recommendation: Same ParseResult, same walker, just skip OffsetTable/ArrayBuffer population. IsValid works, indexers return Empty. Simplest approach.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalker" --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INTG-01 | Single-pass validation + offset table construction | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaWalkerTests" -q` | No - Wave 0 |
| INTG-01 | TryParse returns true for valid JSON + valid schema | integration | (same test class) | No - Wave 0 |
| INTG-01 | TryParse returns false for schema-invalid JSON | integration | (same test class) | No - Wave 0 |
| INTG-01 | Parse returns null for malformed JSON | integration | (same test class) | No - Wave 0 |
| INTG-01 | Parse returns ParseResult with errors for schema-invalid JSON | integration | (same test class) | No - Wave 0 |
| INTG-02 | Nested property access result["address"]["street"] | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~NestedPropertyAccessTests" -q` | No - Wave 0 |
| INTG-03 | Array element access result["tags"][0] | integration | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~ArrayElementAccessTests" -q` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaWalkerTests.cs` -- covers INTG-01 (core walker + validation + offset table)
- [ ] `tests/Gluey.Contract.Json.Tests/NestedPropertyAccessTests.cs` -- covers INTG-02 (hierarchical indexers)
- [ ] `tests/Gluey.Contract.Json.Tests/ArrayElementAccessTests.cs` -- covers INTG-03 (array buffer + element indexers)

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of all source files in src/Gluey.Contract/ and src/Gluey.Contract.Json/
- CONTEXT.md decisions from user discussion session
- All validator signatures verified by reading actual source

### Secondary (MEDIUM confidence)
- C# ref struct constraints from .NET 9 language specification (training knowledge, well-established)

### Tertiary (LOW confidence)
- ParsedProperty struct size impact estimate (theoretical, needs Phase 10 benchmarking)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all dependencies already exist in project
- Architecture: HIGH - all integration points verified by reading source, patterns directly match existing code
- Pitfalls: HIGH - ref struct constraints well-understood, all validator signatures verified
- Composition state capture: MEDIUM - straightforward for scalars, container case may need refinement

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable -- no external dependency changes expected)
