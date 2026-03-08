# Architecture Patterns

**Domain:** Zero-allocation, schema-driven byte parser with JSON format driver
**Researched:** 2026-03-08

## Recommended Architecture

The system divides into two phases with distinct allocation profiles: a **compile phase** (runs once per schema, allocations acceptable) and a **parse phase** (runs per request, zero allocations required). This mirrors the architecture proven by Blaze (compiled JSON Schema validator) and simdjson (tape-based parser), adapted to .NET constraints.

```
COMPILE PHASE (once per schema, allocations OK)
================================================
JSON Schema (string/bytes)
    |
    v
SchemaCompiler
    |
    +-- Resolve $ref, allOf/anyOf/oneOf
    +-- Flatten to instruction tree
    +-- Precompute JSON Pointer paths (invariant 3)
    +-- Determine max property count for offset table sizing
    |
    v
CompiledSchema (immutable, reusable across requests)
    |
    +-- SchemaNode tree with precomputed paths
    +-- Validation instructions per node
    +-- Metadata: max properties, max depth, max array items


PARSE PHASE (per request, zero allocations)
================================================
Raw bytes (caller-owned) + CompiledSchema
    |
    v
JsonByteReader (ref struct, stack-only)
    |
    +-- UTF-8 byte scanning, token identification
    +-- Structural validation (well-formed JSON)
    |
    v
SchemaWalker (drives single-pass traversal)
    |
    +-- Walks CompiledSchema + byte stream in lockstep
    +-- Validates constraints as tokens are encountered
    +-- Writes entries to OffsetTable
    +-- Writes failures to ErrorCollector
    |
    v
OffsetTable (pre-sized, no growth)     ErrorCollector (fixed capacity)
    |                                       |
    v                                       v
ParseResult (readonly struct)
    |
    v
caller: result["field"].GetString()  -->  reads from original byte buffer
```

## Component Boundaries

| Component | Package | Responsibility | Allocates? |
|-----------|---------|---------------|------------|
| `SchemaCompiler` | Gluey.Contract.Json | Parses JSON Schema, resolves references, produces `CompiledSchema` | Yes (once) |
| `CompiledSchema` | Gluey.Contract | Immutable tree of `SchemaNode` with precomputed paths and validation rules | Heap-allocated, long-lived |
| `JsonByteReader` | Gluey.Contract.Json | Low-level UTF-8 byte scanner; identifies tokens, skips whitespace, validates structural JSON | No (ref struct) |
| `SchemaWalker` | Gluey.Contract.Json | Orchestrates single-pass traversal: reads tokens via `JsonByteReader`, validates against `CompiledSchema`, populates `OffsetTable` | No |
| `OffsetTable` | Gluey.Contract | Maps property paths to byte positions; pre-sized from schema metadata | No (pre-allocated) |
| `ErrorCollector` | Gluey.Contract | Collects validation errors up to configurable max (default 64) | No (pre-allocated) |
| `ParsedProperty` | Gluey.Contract | Readonly struct accessor: offset + length + path into byte buffer; materializes values on demand | No |
| `ParseResult` | Gluey.Contract | Readonly struct wrapping `OffsetTable` + error state; returned to caller | No |
| `ValidationError` | Gluey.Contract | Readonly struct: JSON Pointer path + error code + message | No (paths from schema, codes are enums, messages are const) |

### Package Boundary Rule

`Gluey.Contract` (core) defines the **shapes** -- `ParsedProperty`, `OffsetTable`, `ErrorCollector`, `ValidationError`, `ParseResult`, `CompiledSchema`, `SchemaNode`. It has zero knowledge of JSON syntax.

`Gluey.Contract.Json` (format driver) provides the **JSON-specific** implementations -- `JsonByteReader`, `JsonSchemaCompiler`, and the `SchemaWalker` that wires them together. It depends on core, never the reverse.

Future format drivers (Protobuf, etc.) would implement their own byte reader and schema compiler, reusing the same core types.

## Internal Data Structures

### 1. SchemaNode (compile-time, heap-allocated)

The compiled schema is a tree of `SchemaNode` instances. Each node holds everything the parser needs to validate a single position in the document without runtime lookups.

```csharp
// Lives in Gluey.Contract (core)
// Allocated once during schema compilation, reused across all parse calls
public sealed class SchemaNode
{
    public string Path { get; }                    // Precomputed JSON Pointer, e.g., "/devices/0/name"
    public JsonType ExpectedType { get; }          // Object, Array, String, Number, Boolean, Null
    public bool IsRequired { get; }
    public SchemaNode[]? Properties { get; }       // Child nodes for object properties
    public string[]? PropertyNames { get; }        // UTF-8 comparable names, parallel to Properties
    public SchemaNode? Items { get; }              // Schema for array items
    public ValidationRule[] Rules { get; }         // Constraints: minLength, pattern, minimum, etc.
    public int MaxPropertyCount { get; }           // For sizing OffsetTable
    public int MaxDepth { get; }                   // For stack safety
}
```

**Why a class, not a struct:** `SchemaNode` is allocated once at compile time and shared across all parse invocations. It is a long-lived, graph-structured object (properties reference child nodes, `$ref` can create cycles). Using a class here is correct -- heap allocation happens once, not per parse. The zero-allocation invariant applies to the parse path only.

### 2. OffsetTable (parse-time, zero allocation)

The offset table maps schema-defined property positions to byte ranges in the input buffer. Since the schema is known ahead of time, the maximum number of entries is deterministic.

**Strategy: Pre-allocated array rental from ArrayPool.**

```csharp
// Conceptual structure -- actual implementation uses rented array
public struct OffsetEntry  // 16 bytes per entry
{
    public int Offset;     // Start position in byte buffer
    public int Length;     // Byte length of the value
    public byte Flags;     // Type tag + found/missing marker
    // 3 bytes padding
}
```

**Why ArrayPool over stackalloc or InlineArray:**

- **stackalloc** -- Cannot be used because `readonly struct` constraint on `ParseResult` means it must be returnable from methods and storable in fields. Stack memory dies when the method returns. Also, deeply nested schemas could blow the stack.
- **InlineArray (C# 12)** -- Attractive for small fixed sizes, but the offset table size varies per schema (a schema with 5 properties needs 5 slots; one with 200 needs 200). InlineArray requires compile-time constant sizes. Using the max possible size wastes stack space for small schemas.
- **ArrayPool\<OffsetEntry\>** -- Rents a buffer sized to `SchemaNode.MaxPropertyCount`. The rental itself is zero-allocation (pool returns existing arrays). The caller must return the array via `Dispose()` or a `using` pattern on `ParseResult`.

**Lookup strategy:** Property indices are assigned at schema compile time. When the schema compiler encounters property `"name"` as the 3rd property of an object, it assigns index 2. During parsing, when the byte reader encounters key `"name"`, the walker looks up the index from `SchemaNode.PropertyNames` and writes directly to `OffsetTable[2]`. This is O(1) write, no hashing at runtime.

For property name matching during parse, use **byte-sequence comparison** against the precomputed UTF-8 property names in `SchemaNode.PropertyNames`. For objects with more than ~8 properties, precompute a minimal perfect hash at schema compile time (following Blaze's approach) to avoid linear scans.

### 3. ErrorCollector (parse-time, zero allocation)

```csharp
// Fixed-capacity error buffer, rented from ArrayPool
public struct ErrorCollector : IDisposable
{
    private ValidationError[] _errors;  // Rented from ArrayPool
    private int _count;
    private readonly int _maxErrors;    // Default 64

    public bool Add(ValidationError error) { ... }  // Returns false when full
    public ReadOnlySpan<ValidationError> Errors => _errors.AsSpan(0, _count);
}
```

**Why this works without allocation:** `ValidationError` is a readonly struct whose fields are all non-allocating:
- `Path` -- a `string` reference to the precomputed path on `SchemaNode` (no new string)
- `Code` -- an enum value (e.g., `ValidationErrorCode.TypeMismatch`)
- `Message` -- a const/static string per error code (no interpolation, no formatting)

The error array is rented from `ArrayPool<ValidationError>` and returned when `ParseResult` is disposed.

### 4. JsonByteReader (parse-time, ref struct)

```csharp
// Lives in Gluey.Contract.Json
// ref struct because it wraps ReadOnlySpan<byte>
public ref struct JsonByteReader
{
    private ReadOnlySpan<byte> _buffer;
    private int _position;
    private JsonTokenType _tokenType;

    public bool Read();                           // Advance to next token
    public JsonTokenType TokenType { get; }       // Current token type
    public ReadOnlySpan<byte> ValueSpan { get; }  // Raw bytes of current value
    public int TokenStart { get; }                // Byte offset of current token
    public int TokenLength { get; }               // Byte length of current value
}
```

**Design rationale:** This is deliberately simpler than `System.Text.Json.Utf8JsonReader`. We do not need streaming support (`ReadOnlySequence<byte>`), comments, trailing commas, or configurable max depth -- the schema defines max depth. A purpose-built reader avoids the overhead of features we will never use.

**Why not wrap Utf8JsonReader directly:** `Utf8JsonReader` is a ref struct that cannot be stored in fields or passed by reference easily. More importantly, it does not expose raw byte offsets in a way that maps cleanly to our offset table (it processes and unescapes strings internally). Building our own reader gives us direct control over offset tracking, which is the entire point of the library.

### 5. SchemaWalker (the orchestrator)

The `SchemaWalker` is the central parse-time coordinator. It drives the `JsonByteReader` forward while walking the `CompiledSchema` tree, validating constraints and populating the offset table.

```
SchemaWalker.Walk(reader, schemaNode, offsetTable, errorCollector)
    |
    +-- reader.Read()  -->  get next token
    +-- Match token type against schemaNode.ExpectedType
    +-- If object:
    |       for each key token:
    |           find property index in schemaNode.PropertyNames
    |           recurse: Walk(reader, schemaNode.Properties[index], ...)
    |           write OffsetEntry at that index
    +-- If array:
    |       for each element:
    |           recurse: Walk(reader, schemaNode.Items, ...)
    +-- If primitive:
    |       validate constraints (Rules)
    |       write OffsetEntry
    +-- On constraint violation:
            errorCollector.Add(new ValidationError(schemaNode.Path, code, message))
```

**Single-pass guarantee:** The walker never backtracks. It reads each byte exactly once. Validation happens as tokens are encountered, not in a separate pass.

## Data Flow

### Compile Flow (once per application lifetime)

```
JSON Schema string/bytes
    |
    [JsonSchemaCompiler.Compile()]
    |
    +-- Parse JSON Schema document (System.Text.Json or manual parse -- allocation OK here)
    +-- Resolve $ref references (inline or lazy)
    +-- Build SchemaNode tree
    +-- Precompute JSON Pointer path strings for every node
    +-- Compute MaxPropertyCount, MaxDepth for each subtree
    +-- For objects with >8 properties: compute minimal perfect hash table
    +-- Compile validation rules into ValidationRule[] per node
    |
    v
CompiledSchema (thread-safe, immutable, cacheable)
```

### Parse Flow (per request, zero allocation)

```
Step 1: Rent resources
    offsetEntries = ArrayPool<OffsetEntry>.Shared.Rent(schema.MaxPropertyCount)
    errorBuffer = ArrayPool<ValidationError>.Shared.Rent(maxErrors)

Step 2: Create stack-local working structures
    var reader = new JsonByteReader(inputBytes)
    var offsetTable = new OffsetTable(offsetEntries, schema.MaxPropertyCount)
    var errors = new ErrorCollector(errorBuffer, maxErrors)

Step 3: Single-pass walk
    SchemaWalker.Walk(ref reader, schema.Root, ref offsetTable, ref errors)

Step 4: Build result (readonly struct, cheap copy)
    return new ParseResult(offsetTable, errors, inputBytes, schema)
    // ParseResult owns the rented arrays; Dispose() returns them to pool

Step 5: Caller accesses values
    result["deviceName"].GetString()
    // Reads bytes at offsetTable["deviceName"].Offset..Length from inputBytes
    // String created only when caller explicitly requests it
```

## Patterns to Follow

### Pattern 1: Two-Phase Architecture (Compile vs Parse)

**What:** Separate expensive schema processing (heap allocations, string building, reference resolution) from the hot parse path (zero allocation).

**When:** Always. This is the fundamental architectural pattern.

**Why:** Proven by simdjson (compile structural index, then parse), Blaze (compile schema to instructions, then validate), and FlatBuffers (compile schema to accessors). The compile phase amortizes over thousands of parse invocations.

### Pattern 2: Schema-Driven Sizing

**What:** Use schema metadata to pre-size all buffers. The schema knows the maximum number of properties, maximum nesting depth, and maximum array sizes (when `maxItems` is specified).

**When:** Always for offset tables and error collectors.

**Why:** Eliminates dynamic growth (which requires allocation). The schema is the contract -- if the input exceeds schema bounds, that is a validation error, not a resize trigger.

```csharp
// Schema compilation precomputes sizes
int maxProps = schemaNode.MaxPropertyCount;  // e.g., 12
var entries = ArrayPool<OffsetEntry>.Shared.Rent(maxProps);  // Rents >= 12
```

### Pattern 3: Index-Based Property Lookup

**What:** Assign each schema property a compile-time integer index. During parse, write offset entries directly to that index position.

**When:** For all object property access.

**Why:** Avoids runtime dictionary lookups, string hashing, and hash table allocation. The offset table becomes a flat array indexed by property ordinal.

```csharp
// At compile time: "name" -> index 0, "age" -> index 1, "email" -> index 2
// At parse time:
offsetTable[0] = new OffsetEntry(offset: 15, length: 7, flags: Found | String);
// At access time:
result["name"]  -->  schema lookup: "name" -> 0  -->  offsetTable[0]
```

### Pattern 4: Precomputed Paths (Invariant 3)

**What:** Every `SchemaNode` carries its full JSON Pointer path as a pre-allocated string. Validation errors reference this string by pointer, never constructing paths at parse time.

**When:** Always. Path construction requires string concatenation, which allocates.

**Why:** The schema defines all possible paths. For arrays, precompute paths for indices 0-N (where N = `maxItems` or a reasonable default like 64). If an array exceeds precomputed paths, fall back to a cached/pooled path builder.

### Pattern 5: ArrayPool for Variable-Size Buffers

**What:** Rent arrays from `ArrayPool<T>.Shared` for the offset table and error collector. Return them when `ParseResult` is disposed.

**When:** For any buffer whose size depends on the schema (varies per endpoint).

**Why:** `stackalloc` cannot survive method return boundaries. `InlineArray` requires compile-time constant sizes. ArrayPool provides zero-allocation buffer rental with O(1) rent/return.

```csharp
public readonly struct ParseResult : IDisposable
{
    private readonly OffsetEntry[] _entries;  // Rented
    private readonly ValidationError[] _errors;  // Rented

    public void Dispose()
    {
        ArrayPool<OffsetEntry>.Shared.Return(_entries);
        ArrayPool<ValidationError>.Shared.Return(_errors);
    }
}
```

### Pattern 6: Const/Static Error Messages

**What:** Validation error messages are static strings, not formatted at runtime.

**When:** Always. String interpolation allocates.

**Why:** Error messages are deterministic from the error code. `"Value must be of type 'string'"` is always the same string. Store them in a static lookup table indexed by `ValidationErrorCode`.

```csharp
internal static class ErrorMessages
{
    private static readonly string[] Messages = new string[32];  // One per error code

    static ErrorMessages()
    {
        Messages[(int)ValidationErrorCode.TypeMismatch] = "Value type does not match schema";
        Messages[(int)ValidationErrorCode.Required] = "Required property is missing";
        // ...
    }

    public static string Get(ValidationErrorCode code) => Messages[(int)code];
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Dictionary-Based Offset Table

**What:** Using `Dictionary<string, OffsetEntry>` to map property names to byte positions.

**Why bad:** Dictionary allocates bucket arrays, entry arrays, and potentially resizes. Even if pre-sized, the string key lookups allocate during comparison in some paths. Defeats zero-allocation goal.

**Instead:** Use a flat array indexed by compile-time property ordinals. Property name to index mapping is resolved at schema compile time.

### Anti-Pattern 2: String Interpolation in Error Messages

**What:** `$"Expected {expectedType} at {path} but got {actualType}"`

**Why bad:** Every interpolation creates a new string on the heap. With 10 validation errors, that is 10 allocations.

**Instead:** Use static message strings. The path comes from `SchemaNode.Path` (precomputed). The type information can be communicated via the error code.

### Anti-Pattern 3: Wrapping System.Text.Json.Utf8JsonReader

**What:** Using `Utf8JsonReader` internally and extracting offsets from it.

**Why bad:** `Utf8JsonReader` is a ref struct with complex internal state (multi-segment support, comment handling, max depth tracking). It does not expose raw byte offsets for unprocessed values -- it unescapes strings and parses numbers internally. Trying to extract raw offsets would require fighting the API.

**Instead:** Build a purpose-built `JsonByteReader` that tracks byte positions natively. It is simpler (no streaming, no comments) and directly serves the offset table use case.

### Anti-Pattern 4: Recursive Descent with Stack Frames Per Property

**What:** Each property parse creates a new stack frame via method recursion, with local variables for each level.

**Why bad:** Deep nesting (e.g., 50 levels) consumes significant stack space. Combined with `stackalloc` buffers, risks stack overflow.

**Instead:** Use an explicit traversal stack (rented from ArrayPool) for nesting. The `SchemaWalker` maintains a `Span<WalkFrame>` where each frame is a small struct (schema node reference, property index, state). This bounds stack usage regardless of nesting depth.

### Anti-Pattern 5: Allocating ValidationError with Dynamic Data

**What:** Storing computed values in error structs (e.g., "got value 'abc' but max length is 5").

**Why bad:** Including the actual value in the error message requires reading bytes, converting to string, and interpolating -- all allocations.

**Instead:** Error structs carry: path (precomputed), code (enum), message (static). The caller can read the actual value from the byte buffer via the offset table if they need it for logging.

## Scalability Considerations

| Concern | Small Payload (10 props) | Medium Payload (100 props) | Large Payload (1000+ props) |
|---------|-------------------------|---------------------------|----------------------------|
| Offset table size | ~160 bytes (rented) | ~1.6 KB (rented) | ~16 KB (rented) |
| Property lookup | Linear scan of PropertyNames (fast for <=8) | Minimal perfect hash | Minimal perfect hash |
| Nesting depth | Direct recursion OK | Direct recursion OK | Explicit walk stack (rented) |
| Error collection | 64 * ~32 bytes = ~2 KB (rented) | Same | Same (capped at max) |
| Schema compile time | Microseconds | Low milliseconds | Low milliseconds |
| Parse time per request | Sub-microsecond | Low microseconds | Tens of microseconds |

## Suggested Build Order

Components have clear dependency chains. Build order should follow these dependencies.

### Phase 1: Core Types (Gluey.Contract)

Build the foundational readonly structs that everything else depends on.

1. **ValidationErrorCode** (enum) -- error codes with static messages
2. **ValidationError** (readonly struct) -- path + code + message
3. **OffsetEntry** (readonly struct) -- offset + length + flags
4. **ParsedProperty** (readonly struct) -- wraps OffsetEntry + byte[] reference + path
5. **OffsetTable** (struct) -- wraps rented array, provides indexer
6. **ErrorCollector** (struct) -- wraps rented array, provides Add()
7. **ParseResult** (readonly struct, IDisposable) -- wraps OffsetTable + ErrorCollector + byte reference

**Why first:** Every other component produces or consumes these types. They are the contract between core and format drivers.

### Phase 2: Schema Model (Gluey.Contract)

Build the schema representation that the compiler targets and the walker consumes.

1. **JsonType** (enum/flags) -- String, Number, Integer, Boolean, Null, Object, Array
2. **ValidationRule** (readonly struct) -- constraint kind + parameters (min, max, pattern ref, etc.)
3. **SchemaNode** (sealed class) -- tree node with path, type, rules, children, metadata

**Why second:** The schema model defines the "shape" that both the compiler and walker operate on. It must be stable before either can be built.

### Phase 3: JSON Byte Reader (Gluey.Contract.Json)

Build the low-level UTF-8 tokenizer.

1. **JsonTokenType** (enum) -- StartObject, EndObject, StartArray, EndArray, PropertyName, String, Number, True, False, Null
2. **JsonByteReader** (ref struct) -- byte-by-byte scanner with token identification

**Why third:** The walker needs the reader to traverse bytes, but the reader has no dependency on schema or offset tables. It is a pure tokenizer.

### Phase 4: Schema Compiler (Gluey.Contract.Json)

Build the JSON Schema compiler that produces `CompiledSchema`.

1. **JsonSchemaCompiler** -- parses JSON Schema, resolves $ref, builds SchemaNode tree
2. Path precomputation logic
3. Property index assignment
4. MaxPropertyCount / MaxDepth calculation

**Why fourth:** The compiler depends on the schema model (Phase 2) but not on the reader or walker. It can be tested independently by inspecting the compiled output.

### Phase 5: Schema Walker + Integration (Gluey.Contract.Json)

Wire everything together in the single-pass walker.

1. **SchemaWalker** -- orchestrates reader + schema + offset table + error collector
2. **JsonContractSchema** -- public API surface (TryParse / Parse methods)
3. Integration tests with real JSON payloads

**Why last:** The walker depends on every prior component. It is the integration point where zero-allocation guarantees are verified end-to-end.

## Sources

- [simdjson tape structure](https://github.com/simdjson/simdjson/blob/master/doc/tape.md) -- tape data structure design, 64-bit element encoding
- [Blaze: Compiling JSON Schema for 10x Faster Validation](https://arxiv.org/html/2503.02770v1) -- compiled instruction set, schema-to-DSL compilation, semi-perfect hashing
- [jsmn parser](https://zserge.com/jsmn/) -- token array with offset-based zero-copy design
- [flatjson](https://github.com/niXman/flatjson) -- index overlay approach, zero allocation and zero copy
- [System.Text.Json Utf8JsonReader](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) -- ref struct reader design, zero-allocation token-by-token reading
- [Corvus.JsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema) -- .NET JSON Schema code generation with zero/low allocation validation
- [.NET InlineArray](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/inline-arrays) -- C# 12 fixed-size struct arrays, limitations for variable-size use
- [ArrayPool in .NET](https://adamsitnik.com/Array-Pool/) -- zero-allocation buffer rental patterns
- [Utf8Json](https://github.com/neuecc/Utf8Json) -- zero-allocation JSON serializer architecture for .NET

**Confidence levels:**
- Component boundaries and data flow: HIGH -- derived from project ADRs, invariants, and established parser architectures (simdjson, jsmn, Blaze)
- ArrayPool strategy for offset tables: HIGH -- standard .NET pattern, well-documented, aligns with readonly struct constraint (ADR 8)
- Property index lookup via compile-time ordinals: HIGH -- used by simdjson (tape indices), FlatBuffers (field offsets), and Blaze (property hashing)
- Custom JsonByteReader over Utf8JsonReader: MEDIUM -- architectural judgment; Utf8JsonReader could work but would fight the offset-tracking use case
- Minimal perfect hash for large property sets: MEDIUM -- Blaze demonstrates the approach but .NET implementation details need validation during build
