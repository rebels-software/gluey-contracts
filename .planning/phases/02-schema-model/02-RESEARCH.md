# Phase 2: Schema Model - Research

**Researched:** 2026-03-09
**Domain:** JSON Schema Draft 2020-12 schema model, Utf8JsonReader parsing, RFC 6901 JSON Pointer paths, property index assignment
**Confidence:** HIGH

## Summary

Phase 2 builds the internal schema model: an immutable `SchemaNode` tree parsed from JSON Schema Draft 2020-12 documents using `System.Text.Json.Utf8JsonReader`. The schema is loaded via static factory methods (`TryLoad`/`Load`) on the existing `JsonContractSchema` class. Each node carries a precomputed RFC 6901 JSON Pointer path (string allocated once at load time, reused at zero cost during parse). Named object properties receive depth-first ordinal indices that size the `OffsetTable` and populate the `Dictionary<string, int>` consumed by `ParseResult`.

JSON Schema Draft 2020-12 defines approximately 50 keywords across 7 vocabularies. All keywords are modeled as nullable fields on `SchemaNode` and parsed during load. `$ref`/`$defs`/`$anchor` strings are stored but resolution logic is deferred to Phase 3. The loader uses `Utf8JsonReader` directly (no `JsonDocument` or `JsonNode` intermediate) to keep the BCL-only constraint and minimize allocations during the one-time schema load.

**Primary recommendation:** Model SchemaNode as an internal sealed class with ~30 nullable fields covering all Draft 2020-12 keywords. Use `Utf8JsonReader` in a single recursive-descent pass to populate the tree. Assign property ordinals via depth-first traversal after the tree is built.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Static factory methods on JsonContractSchema: `TryLoad` (bool + out) and `Load` (returns nullable)
- Dual API mirrors TryParse/Parse pattern from Phase 1 -- consistent philosophy across the library
- Accept `ReadOnlySpan<byte>` for UTF-8 bytes and `string` for JSON text -- two overloads per method
- Use System.Text.Json's Utf8JsonReader internally to parse the JSON Schema document -- BCL dependency, no external packages
- Schema loading is a one-time setup cost; zero-allocation invariant applies to parse path, not schema loading
- SchemaNode is internal sealed class -- not part of public API, free to refactor internals
- SchemaNode lives in `Gluey.Contract` (core package) -- schema model is format-agnostic per ADR 5
- JSON Schema loader lives in `Gluey.Contract.Json` -- parses JSON Schema format into the core SchemaNode tree
- Immutable by design -- all properties set in constructor, never mutated after construction
- Class (not readonly struct) because tree nodes reference children; allocated once at load time, not on parse path
- Depth-first traversal order for ordinal assignment -- natural for single-pass walker descent
- JSON Pointer paths as dictionary keys for name-to-ordinal mapping (e.g., `"/address/street"` -> ordinal 2)
- Only named object properties get ordinals -- arrays tracked as a whole, individual elements resolved at parse time by walker (Phase 9)
- JsonContractSchema exposes a `PropertyCount` for pre-sizing the offset table
- All JSON Schema Draft 2020-12 keywords defined as fields on SchemaNode upfront (~30 nullable fields)
- Loader parses ALL keywords from JSON during Load -- validation phases just consume what's already on the node
- $ref string and $defs map read and stored in Phase 2; resolution logic deferred to Phase 3
- Unknown/unrecognized keywords silently ignored -- standard JSON Schema behavior

### Claude's Discretion
- Exact SchemaNode field types and nullability strategy (nullable value types vs sentinel values)
- Internal organization of the JSON Schema loader (single class vs per-section helpers)
- SchemaType enum values and how multi-type schemas are represented
- How precomputed path strings are allocated and shared across nodes

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCHM-01 | Schema loading from JSON bytes and JSON string | TryLoad/Load factory methods using Utf8JsonReader; ReadOnlySpan<byte> and string overloads |
| SCHM-02 | SchemaNode immutable tree with precomputed JSON Pointer paths | Internal sealed class with constructor-set fields; RFC 6901 paths built during tree construction |
| SCHM-05 | Property index assignment for zero-allocation offset table sizing | Depth-first traversal assigns ordinals to named properties; PropertyCount exposed on JsonContractSchema |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | BCL (net9.0) | Utf8JsonReader for JSON Schema parsing | BCL dependency, no external packages per ADR 7; high-performance forward-only reader |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework (established in Phase 1) | All test classes |
| FluentAssertions | 8.0.1 | Assertion library (established in Phase 1) | All test assertions |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Utf8JsonReader | JsonDocument/JsonNode | JsonDocument allocates a DOM; Utf8JsonReader is zero-copy forward-only. Schema loading is one-time cost so either would work, but Utf8JsonReader is more consistent with the library's philosophy |

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract/
    SchemaNode.cs              # Internal sealed class -- the immutable tree node
    SchemaType.cs              # [Flags] enum for JSON Schema type keyword
src/Gluey.Contract.Json/
    JsonContractSchema.cs      # Add TryLoad/Load factory methods + PropertyCount
    JsonSchemaLoader.cs        # Internal static class -- Utf8JsonReader -> SchemaNode tree
tests/Gluey.Contract.Json.Tests/
    JsonSchemaLoadingTests.cs   # TryLoad/Load API tests
    SchemaNodeTests.cs          # Tree structure, paths, ordinals (via InternalsVisibleTo)
```

### Pattern 1: Recursive-Descent Loader with Utf8JsonReader
**What:** A single internal static method that reads a JSON Schema document token-by-token using Utf8JsonReader, recursively constructing SchemaNode children when encountering sub-schemas (properties, items, allOf, etc.).
**When to use:** Always -- this is the only loader pattern needed.
**Example:**
```csharp
// Source: Microsoft Learn Utf8JsonReader docs
internal static class JsonSchemaLoader
{
    internal static SchemaNode? Load(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return null;

        return ReadSchemaObject(ref reader, "/");
    }

    private static SchemaNode ReadSchemaObject(ref Utf8JsonReader reader, string currentPath)
    {
        // Temporary holders for keyword values
        SchemaType? type = null;
        Dictionary<string, SchemaNode>? properties = null;
        SchemaNode? items = null;
        // ... ~30 nullable keyword fields

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            // Use ValueTextEquals for zero-alloc property name matching
            if (reader.ValueTextEquals("type"u8))
            {
                reader.Read();
                type = ReadType(ref reader);
            }
            else if (reader.ValueTextEquals("properties"u8))
            {
                reader.Read();
                properties = ReadProperties(ref reader, currentPath);
            }
            // ... handle all keywords
            else
            {
                // Unknown keyword -- skip silently
                reader.Read();
                reader.Skip(); // skip value
            }
        }

        return new SchemaNode(currentPath, type, properties, items, /* ... */);
    }
}
```

### Pattern 2: RFC 6901 JSON Pointer Path Construction
**What:** Build path strings incrementally during tree construction. Each child appends `"/" + escapedPropertyName` to the parent path.
**When to use:** During SchemaNode construction for every node in the tree.
**Example:**
```csharp
// RFC 6901: ~ encoded as ~0, / encoded as ~1
private static string BuildChildPath(string parentPath, string propertyName)
{
    // Root path is ""
    // Properties: "/propertyName"
    // Nested: "/address/street"
    string escaped = propertyName
        .Replace("~", "~0")
        .Replace("/", "~1");
    return parentPath == "/"
        ? "/" + escaped
        : parentPath + "/" + escaped;
}
```

### Pattern 3: Depth-First Ordinal Assignment
**What:** After the SchemaNode tree is built, walk it depth-first to assign stable integer ordinals to named object properties only.
**When to use:** As a post-construction pass, producing the `Dictionary<string, int>` and `PropertyCount`.
**Example:**
```csharp
internal static (Dictionary<string, int> nameToOrdinal, int propertyCount)
    AssignOrdinals(SchemaNode root)
{
    var mapping = new Dictionary<string, int>();
    int ordinal = 0;
    AssignOrdinalsRecursive(root, mapping, ref ordinal);
    return (mapping, ordinal);
}

private static void AssignOrdinalsRecursive(
    SchemaNode node,
    Dictionary<string, int> mapping,
    ref int ordinal)
{
    if (node.Properties is not null)
    {
        foreach (var (name, child) in node.Properties)
        {
            mapping[child.Path] = ordinal++;
            AssignOrdinalsRecursive(child, mapping, ref ordinal);
        }
    }
    // items, prefixItems, allOf, etc. -- recurse into sub-schemas
    // but do NOT assign ordinals to array items (Phase 9 handles those)
}
```

### Pattern 4: SchemaType as Flags Enum
**What:** JSON Schema `type` can be a single string or an array of strings. Use a `[Flags]` enum to represent multi-type schemas compactly.
**When to use:** On the `SchemaNode.Type` field.
**Example:**
```csharp
[Flags]
internal enum SchemaType : byte
{
    None    = 0,
    Null    = 1 << 0,  // 1
    Boolean = 1 << 1,  // 2
    Integer = 1 << 2,  // 4
    Number  = 1 << 3,  // 8
    String  = 1 << 4,  // 16
    Array   = 1 << 5,  // 32
    Object  = 1 << 6,  // 64
}
```
**Rationale:** A byte-sized flags enum fits 7 JSON Schema types. Multi-type schemas like `["string", "null"]` become `SchemaType.String | SchemaType.Null`. Validation checks use bitwise AND for O(1) type matching.

### Anti-Patterns to Avoid
- **Using JsonDocument as intermediate:** Allocates a full DOM; unnecessary for schema loading where we only need to walk once
- **Mutable SchemaNode fields:** All keyword values must be set in the constructor; post-construction mutation breaks immutability guarantees
- **Allocating path strings during parse (validation):** Paths are precomputed at schema load time and stored on SchemaNode; validation just reads them
- **String-based type checking:** Use the flags enum, not string comparisons, for type validation
- **Recursive Utf8JsonReader without ref passing:** Utf8JsonReader is a ref struct; always pass by ref to avoid state copy issues

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON tokenization | Custom JSON lexer | Utf8JsonReader | BCL handles escaping, UTF-8, BOM stripping, error positions; battle-tested |
| UTF-8 string encoding | Manual byte manipulation | Encoding.UTF8 / reader.GetString() | Schema loading is one-time; use BCL string APIs freely |
| JSON Pointer escaping | Ad-hoc string replacement | Centralized `EscapePointerToken` method | RFC 6901 escaping order matters (~0 before ~1 on decode, reverse on encode) |

**Key insight:** Schema loading is a one-time setup cost. The zero-allocation constraint applies to the parse/validation path (Phase 9), not to schema loading. Use allocating APIs freely when building the schema model.

## Common Pitfalls

### Pitfall 1: Utf8JsonReader ref struct Semantics
**What goes wrong:** Passing Utf8JsonReader by value to helper methods; the copy doesn't advance the original reader.
**Why it happens:** Utf8JsonReader is a mutable ref struct. Value copies create independent readers.
**How to avoid:** Always pass by `ref`. Every helper method signature must use `ref Utf8JsonReader reader`.
**Warning signs:** Reader appears to "re-read" tokens; infinite loops in parsing.

### Pitfall 2: RFC 6901 Escape Order
**What goes wrong:** Encoding `~` and `/` in the wrong order corrupts paths.
**Why it happens:** `~0` contains `~` which could be double-escaped if order is wrong.
**How to avoid:** When encoding (building paths): replace `~` with `~0` first, then `/` with `~1`. When decoding: replace `~1` with `/` first, then `~0` with `~`.
**Warning signs:** Property names containing `~` or `/` produce incorrect paths.

### Pitfall 3: JSON Schema `type` as String or Array
**What goes wrong:** Loader assumes `type` is always a string; crashes on `"type": ["string", "null"]`.
**Why it happens:** JSON Schema allows both `"type": "string"` and `"type": ["string", "null"]`.
**How to avoid:** Check `reader.TokenType` -- if `JsonTokenType.String`, read single type; if `JsonTokenType.StartArray`, read array of types. Map to `SchemaType` flags enum.
**Warning signs:** Nullable type schemas fail to load.

### Pitfall 4: Keyword Value Types Are Not Uniform
**What goes wrong:** Treating all keyword values as simple scalars.
**Why it happens:** Some keywords accept schemas (object/boolean), some accept arrays of schemas, some accept string arrays, some accept integers.
**How to avoid:** Reference the keyword type table below; each keyword needs its own reader method.
**Warning signs:** Loader crashes or silently drops nested schemas.

### Pitfall 5: Boolean Schemas
**What goes wrong:** Only handling schema objects, not `true`/`false` as valid schemas.
**Why it happens:** JSON Schema allows `true` (accept everything) and `false` (reject everything) as valid schemas.
**How to avoid:** Before reading `StartObject`, check for `True`/`False` token types. SchemaNode needs a way to represent boolean schemas (e.g., a `BooleanSchema` sentinel or a flag).
**Warning signs:** `additionalProperties: false` fails to load.

### Pitfall 6: Skip() Behavior on PropertyName
**What goes wrong:** Calling `reader.Skip()` when token is `PropertyName` skips the property value, not the property name.
**Why it happens:** `Skip()` on `PropertyName` advances to and skips the value. This is usually desired but can be surprising.
**How to avoid:** For unknown keywords: call `reader.Read()` to advance to value, then `reader.Skip()` to skip it. Or just call `reader.Skip()` directly on the PropertyName.
**Warning signs:** Off-by-one in token reading.

## Code Examples

### SchemaNode Field Design (Recommended)
```csharp
// Source: JSON Schema Draft 2020-12 vocabulary analysis
namespace Gluey.Contract;

/// <summary>
/// An immutable node in the compiled schema tree.
/// Represents a single JSON Schema (sub)schema with all recognized keywords.
/// </summary>
internal sealed class SchemaNode
{
    // -- Identity / Core --
    internal string Path { get; }                        // RFC 6901 JSON Pointer
    internal string? Id { get; }                         // $id
    internal string? Anchor { get; }                     // $anchor
    internal string? Ref { get; }                        // $ref (string, resolved in Phase 3)
    internal string? DynamicRef { get; }                 // $dynamicRef
    internal string? DynamicAnchor { get; }              // $dynamicAnchor
    internal Dictionary<string, SchemaNode>? Defs { get; }  // $defs
    internal string? Comment { get; }                    // $comment

    // -- Type --
    internal SchemaType? Type { get; }                   // type (flags enum)

    // -- Validation: Any --
    internal JsonElement[]? Enum { get; }                // enum (raw values for byte comparison)
    internal JsonElement? Const { get; }                 // const

    // -- Validation: Numeric --
    internal decimal? Minimum { get; }
    internal decimal? Maximum { get; }
    internal decimal? ExclusiveMinimum { get; }
    internal decimal? ExclusiveMaximum { get; }
    internal decimal? MultipleOf { get; }

    // -- Validation: String --
    internal int? MinLength { get; }
    internal int? MaxLength { get; }
    internal string? Pattern { get; }

    // -- Validation: Array --
    internal int? MinItems { get; }
    internal int? MaxItems { get; }
    internal bool? UniqueItems { get; }
    internal int? MinContains { get; }
    internal int? MaxContains { get; }

    // -- Validation: Object --
    internal string[]? Required { get; }
    internal int? MinProperties { get; }
    internal int? MaxProperties { get; }
    internal Dictionary<string, string[]>? DependentRequired { get; }

    // -- Applicator: Object --
    internal Dictionary<string, SchemaNode>? Properties { get; }
    internal SchemaNode? AdditionalProperties { get; }  // schema or boolean schema
    internal Dictionary<string, SchemaNode>? PatternProperties { get; }
    internal SchemaNode? PropertyNames { get; }
    internal Dictionary<string, SchemaNode>? DependentSchemas { get; }

    // -- Applicator: Array --
    internal SchemaNode? Items { get; }
    internal SchemaNode[]? PrefixItems { get; }
    internal SchemaNode? Contains { get; }

    // -- Applicator: Composition --
    internal SchemaNode[]? AllOf { get; }
    internal SchemaNode[]? AnyOf { get; }
    internal SchemaNode[]? OneOf { get; }
    internal SchemaNode? Not { get; }

    // -- Applicator: Conditional --
    internal SchemaNode? If { get; }
    internal SchemaNode? Then { get; }
    internal SchemaNode? Else { get; }

    // -- Meta-data (annotations, not used for validation) --
    internal string? Title { get; }
    internal string? Description { get; }
    internal bool? Deprecated { get; }
    internal string? Format { get; }

    // -- Boolean schema sentinel --
    internal bool? BooleanSchema { get; }  // true = accept all, false = reject all, null = normal schema

    internal SchemaNode(
        string path,
        // ... all keyword parameters
    )
    {
        Path = path;
        // ... assign all properties
    }
}
```

### TryLoad/Load API on JsonContractSchema
```csharp
// In Gluey.Contract.Json/JsonContractSchema.cs
public class JsonContractSchema
{
    private readonly SchemaNode _root;
    private readonly Dictionary<string, int> _nameToOrdinal;

    /// <summary>Number of named properties in the schema (for OffsetTable sizing).</summary>
    public int PropertyCount { get; }

    private JsonContractSchema(SchemaNode root, Dictionary<string, int> nameToOrdinal, int propertyCount)
    {
        _root = root;
        _nameToOrdinal = nameToOrdinal;
        PropertyCount = propertyCount;
    }

    /// <summary>
    /// Attempts to load a JSON Schema from UTF-8 encoded bytes.
    /// </summary>
    public static bool TryLoad(ReadOnlySpan<byte> utf8Json, out JsonContractSchema? schema)
    {
        var root = JsonSchemaLoader.Load(utf8Json);
        if (root is null) { schema = null; return false; }
        var (mapping, count) = SchemaIndexer.AssignOrdinals(root);
        schema = new JsonContractSchema(root, mapping, count);
        return true;
    }

    /// <summary>
    /// Loads a JSON Schema from UTF-8 encoded bytes. Returns null if loading fails.
    /// </summary>
    public static JsonContractSchema? Load(ReadOnlySpan<byte> utf8Json)
    {
        TryLoad(utf8Json, out var schema);
        return schema;
    }

    // String overloads convert to UTF-8 then delegate
    public static bool TryLoad(string json, out JsonContractSchema? schema)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        return TryLoad(utf8, out schema);
    }

    public static JsonContractSchema? Load(string json)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        return Load(utf8);
    }

    // Existing TryParse/Parse stubs remain unchanged
}
```

### Utf8JsonReader Property Name Matching
```csharp
// Source: Microsoft Learn Utf8JsonReader docs
// Use pre-encoded UTF-8 byte arrays for zero-allocation property name matching
private static ReadOnlySpan<byte> TypeKeyword => "type"u8;
private static ReadOnlySpan<byte> PropertiesKeyword => "properties"u8;
private static ReadOnlySpan<byte> RequiredKeyword => "required"u8;
// ... one per keyword

// In the reader loop:
if (reader.ValueTextEquals(TypeKeyword))
{
    // handle type keyword
}
```

## JSON Schema Draft 2020-12 Keyword Reference

Complete keyword inventory by value type (for loader implementation):

### Keywords accepting a Schema (object or boolean)
`additionalProperties`, `items`, `contains`, `not`, `if`, `then`, `else`, `propertyNames`, `contentSchema`

### Keywords accepting an Array of Schemas
`allOf`, `anyOf`, `oneOf`, `prefixItems`

### Keywords accepting a Map of String -> Schema
`properties`, `patternProperties`, `dependentSchemas`, `$defs`

### Keywords accepting a String
`$id`, `$ref`, `$anchor`, `$comment`, `$schema`, `$dynamicRef`, `$dynamicAnchor`, `pattern`, `format`, `title`, `description`, `contentEncoding`, `contentMediaType`

### Keywords accepting a Number
`minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf`

### Keywords accepting a Non-Negative Integer
`minLength`, `maxLength`, `minItems`, `maxItems`, `minProperties`, `maxProperties`, `minContains`, `maxContains`

### Keywords accepting a Boolean
`uniqueItems`, `deprecated`, `readOnly`, `writeOnly`

### Keywords accepting a String or Array of Strings
`type`

### Keywords accepting an Array of Strings
`required`

### Keywords accepting a Map of String -> Array of Strings
`dependentRequired`

### Keywords accepting Any JSON Value
`const`, `default`

### Keywords accepting an Array of Any JSON Value
`enum`, `examples`

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JsonDocument for read-only DOM | Utf8JsonReader for forward-only parsing | .NET Core 3.0+ | Lower allocations, no DOM overhead |
| Newtonsoft.Json for schema parsing | System.Text.Json BCL | .NET 5+ mainstream | Zero external dependencies |
| Draft-07 $definitions | Draft 2020-12 $defs | 2020 | Keyword renamed; $definitions is legacy |
| Draft-07 dependencies | Draft 2020-12 dependentRequired + dependentSchemas | 2020 | Split into two keywords |
| Draft-07 items (tuple) | Draft 2020-12 prefixItems + items | 2020 | Cleaner tuple vs list-item semantics |

## Open Questions

1. **enum/const Storage Format**
   - What we know: `enum` and `const` need byte-level comparison during validation (VALD-02). Storing as `JsonElement` would work but requires `JsonDocument` (which allocates).
   - What's unclear: Whether to store raw byte slices, use `JsonElement`, or store as pre-serialized UTF-8 byte arrays.
   - Recommendation: Store as `JsonElement` from a single `JsonDocument` kept alive on the schema. Schema loading allows allocations. The `JsonDocument` is allocated once and lives as long as the schema. Alternatively, store raw UTF-8 byte arrays for each enum value -- simpler and avoids `JsonDocument` lifetime management.

2. **Boolean Schema Representation**
   - What we know: `true` and `false` are valid schemas. `additionalProperties: false` is extremely common.
   - What's unclear: Whether to use a nullable bool field on SchemaNode or create static sentinel instances.
   - Recommendation: Use a `bool? BooleanSchema` field on SchemaNode. When non-null, it overrides all other keyword fields. `SchemaNode.True` and `SchemaNode.False` can be static readonly instances.

3. **Root Path Convention**
   - What we know: RFC 6901 defines `""` (empty string) as the pointer to the whole document.
   - What's unclear: Whether root path should be `""` or `"/"`.
   - Recommendation: Use `""` for the root node per RFC 6901. Child paths are `"/propertyName"`, nested are `"/parent/child"`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "ClassName~SchemaLoading" --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCHM-01 | Load schema from UTF-8 bytes via TryLoad | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_FromBytes" -q` | No -- Wave 0 |
| SCHM-01 | Load schema from string via TryLoad | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_FromString" -q` | No -- Wave 0 |
| SCHM-01 | Load schema from UTF-8 bytes via Load | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Load_FromBytes" -q` | No -- Wave 0 |
| SCHM-01 | Load schema from string via Load | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Load_FromString" -q` | No -- Wave 0 |
| SCHM-01 | TryLoad returns false for invalid JSON | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "TryLoad_InvalidJson" -q` | No -- Wave 0 |
| SCHM-02 | SchemaNode tree is immutable (no public setters) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "SchemaNode_Immutable" -q` | No -- Wave 0 |
| SCHM-02 | Root node has empty-string path | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "RootNode_Path" -q` | No -- Wave 0 |
| SCHM-02 | Nested properties have correct RFC 6901 paths | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "NestedProperties_Paths" -q` | No -- Wave 0 |
| SCHM-02 | Path escaping for ~ and / characters | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PathEscaping" -q` | No -- Wave 0 |
| SCHM-05 | Named properties assigned sequential ordinals | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PropertyOrdinals" -q` | No -- Wave 0 |
| SCHM-05 | PropertyCount matches number of named properties | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "PropertyCount" -q` | No -- Wave 0 |
| SCHM-05 | Depth-first traversal order for ordinals | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "DepthFirst_Ordinals" -q` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/JsonSchemaLoadingTests.cs` -- covers SCHM-01
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaNodeTests.cs` -- covers SCHM-02, SCHM-05 (via InternalsVisibleTo)

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn: Utf8JsonReader](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader) - API patterns, ref struct semantics, ValueTextEquals, Skip behavior
- [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12) - Keyword vocabulary reference
- [Learn JSON Schema 2020-12](https://www.learnjsonschema.com/2020-12/) - Complete keyword inventory by vocabulary
- [RFC 6901](https://tools.ietf.org/html/rfc6901) - JSON Pointer specification, escaping rules
- [JSON Schema Validation Vocabulary](https://json-schema.org/draft/2020-12/json-schema-validation) - Validation keyword definitions

### Secondary (MEDIUM confidence)
- Existing codebase analysis: Phase 1 patterns (ParseResult, OffsetTable, ValidationError) for integration contracts

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - BCL System.Text.Json, no external dependencies needed
- Architecture: HIGH - SchemaNode design follows directly from JSON Schema spec; Utf8JsonReader patterns well-documented by Microsoft
- Pitfalls: HIGH - Utf8JsonReader ref struct semantics and RFC 6901 escaping are well-known gotchas with official documentation
- Keyword inventory: HIGH - Complete list from official JSON Schema 2020-12 specification

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable domain, JSON Schema 2020-12 is not changing)
