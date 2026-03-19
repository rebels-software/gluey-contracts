# Phase 2: Contract Model - Research

**Researched:** 2026-03-19
**Domain:** Binary contract JSON loading, structural validation, dependency chain resolution (C# / .NET)
**Confidence:** HIGH

## Summary

Phase 2 implements the `BinaryContractSchema` class in a new `Gluey.Contract.Binary` assembly. The class mirrors `JsonContractSchema`'s TryLoad/Load API pattern, deserializes a binary contract JSON document (via `System.Text.Json`), validates its structure (cycles, missing root, overlapping bits, missing sizes, invalid references), and resolves the dependency chain into a flat ordered field array with precomputed byte offsets and endianness per field.

The codebase already has all the foundational abstractions needed: `ValidationErrorCode` enum (to extend), `ValidationError` struct, `ErrorCollector` (collect-all pattern), and `SchemaNode` as the reference for single-class-with-nullable-fields modeling. `InternalsVisibleTo` for `Gluey.Contract.Binary` and `Gluey.Contract.Binary.Tests` is already configured in the core project. Phase 1 added `_format` and `_endianness` fields to `ParsedProperty` with working binary read paths, so the contract model just needs to produce data that feeds into those existing paths.

**Primary recommendation:** Create `Gluey.Contract.Binary` project with DTOs for JSON deserialization, a `BinaryContractNode` sealed class mirroring the `SchemaNode` pattern, a multi-phase validator (parse -> resolve -> graph -> type-specific), and a chain resolver that produces the ordered field array with absolute byte offsets.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Mirror JsonContractSchema API exactly: TryLoad/Load overloads (ReadOnlySpan<byte> + string), same SchemaRegistry and SchemaOptions optional params
- TryLoad returns bool only (no error details in out param) -- matches JSON behavior
- BinaryContractSchema lives in its own Gluey.Contract.Binary assembly, mirroring Gluey.Contract.Json's separation
- Metadata fields (Id, Name, Version, DisplayName) exposed as properties -- exactly as JsonContractSchema does
- Require `"kind": "binary"` discriminator in contract JSON -- reject contracts without it or with wrong kind
- Collect all errors before returning (not fail-fast) -- consistent with parse-time error collection pattern
- Reuse existing ValidationErrorCode and ValidationError types from Gluey.Contract core -- add new codes (e.g. CyclicDependency, MissingRoot, OverlappingBits, MissingSize, InvalidReference)
- Phased validation order: (1) parse JSON structure, (2) resolve types + sizes, (3) validate graph (cycles, roots, single-child), (4) validate type-specific rules (bit overlap, array count refs, enum ranges)
- Single BinaryContractNode sealed class with nullable fields per type (bitFields, arrayElement, enumValues, etc.) -- mirrors SchemaNode pattern
- Flat ordered array for resolved top-level fields in parse order; struct/array element fields stored separately on their parent node
- Each BinaryContractNode stores its precomputed absolute byte offset after chain resolution -- zero graph work at parse time
- Each node stores precomputed endianness (resolved from contract-level default + per-field override)
- Use System.Text.Json deserializer (JsonSerializer.Deserialize<T>) to parse contract JSON into DTOs, then map to BinaryContractNode -- clean separation, easy to test, allocations acceptable at load time
- Preserve x-prefixed fields (x-error, x-description, etc.) on BinaryContractNode for parse-time error enrichment -- same pattern as JSON schema's x-error support
- Unknown non-x-prefixed fields: follow JSON package behavior

### Claude's Discretion
- Exact DTO class structure for JSON deserialization intermediaries
- Internal helper method organization for validation phases
- How to handle the string overloads of TryLoad/Load (UTF-8 encode then delegate, matching JSON)
- Naming of new ValidationErrorCode enum values

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CNTR-01 | Binary contract JSON loaded and parsed into internal model (BinaryContractNode tree) | DTO deserialization via System.Text.Json, mapping to BinaryContractNode sealed class |
| CNTR-02 | Dependency chain resolved at load time into ordered field array (no graph traversal at parse time) | Topological sort of dependsOn links, absolute byte offset precomputation |
| CNTR-03 | Contract-load validation: exactly one root field (no dependsOn) | Graph validation phase - count fields where DependsOn is null |
| CNTR-04 | Contract-load validation: no cycles in dependency graph | Graph validation phase - cycle detection via visited set during chain walk |
| CNTR-05 | Contract-load validation: each field has at most one child | Graph validation phase - track dependsOn targets, flag duplicates |
| CNTR-06 | Contract-load validation: semi-dynamic array count references valid numeric field earlier in chain | Type-specific validation phase - verify count string resolves to numeric field with lower offset |
| CNTR-07 | Contract-load validation: bit sub-fields do not overlap and fit within container size | Type-specific validation phase - bit range tracking per container |
| CNTR-08 | Contract-load validation: size is explicitly declared on every field | Resolve phase - check Size property presence on every DTO field |
| CNTR-09 | Endianness resolved at load time (contract-level default with per-field override) | Chain resolution step - merge contract.Endianness with field.Endianness |
| CORE-03 | BinaryContractSchema exposes TryLoad/Load static factory methods matching JsonContractSchema pattern | Direct mirror of JsonContractSchema.cs API surface |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | built-in (.NET 9/10) | Deserialize contract JSON into DTOs | Already used in codebase (Utf8JsonReader in JSON schema loader); JsonSerializer.Deserialize for DTO mapping is the decided approach |
| Gluey.Contract (core) | 1.1.0 | ValidationErrorCode, ValidationError, ErrorCollector, ParsedProperty | Existing core types to extend and reuse |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 4.3.1 | Test framework | All tests (matches existing test projects) |
| FluentAssertions | 8.0.1 | Test assertions | All test assertions (matches existing test projects) |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test host | Required for test execution |
| NUnit3TestAdapter | 4.6.0 | Test adapter | Required for dotnet test |

**Installation:** No new packages needed. New csproj files reference `Gluey.Contract` as a project dependency, test project mirrors `Gluey.Contract.Json.Tests.csproj` package references.

## Architecture Patterns

### Recommended Project Structure
```
src/Gluey.Contract.Binary/
  Gluey.Contract.Binary.csproj
  Schema/
    BinaryContractSchema.cs      # Public API: TryLoad/Load, metadata properties
    BinaryContractNode.cs         # Internal sealed class: single node with nullable fields
    BinaryContractLoader.cs       # Internal: JSON -> DTO -> BinaryContractNode tree
    BinaryContractValidator.cs    # Internal: multi-phase validation
    BinaryChainResolver.cs        # Internal: dependency chain -> ordered array + offsets
  Dto/
    ContractDto.cs                # Internal: System.Text.Json deserialization target
    FieldDto.cs                   # Internal: per-field DTO
    BitFieldDto.cs                # Internal: bit sub-field DTO
    ArrayElementDto.cs            # Internal: array element DTO
    ValidationDto.cs              # Internal: validation rules DTO

tests/Gluey.Contract.Binary.Tests/
  Gluey.Contract.Binary.Tests.csproj
  GlobalUsings.cs
  ContractLoadingTests.cs         # TryLoad/Load API tests
  ContractValidationTests.cs      # All CNTR-03..08 validation rule tests
  ChainResolutionTests.cs         # CNTR-02 ordering + offset tests
  EndiannessResolutionTests.cs    # CNTR-09 endianness tests
```

### Pattern 1: DTO-then-Map Loading
**What:** Deserialize contract JSON into plain DTO classes using `JsonSerializer.Deserialize<ContractDto>()`, then map DTO tree to `BinaryContractNode` tree in a separate step.
**When to use:** Always -- this is a locked decision.
**Example:**
```csharp
// Source: Mirrors JsonSchemaLoader pattern, adapted for binary contracts
internal static class BinaryContractLoader
{
    internal static BinaryContractNode? Load(ReadOnlySpan<byte> utf8Json, out ErrorCollector errors)
    {
        errors = new ErrorCollector();

        ContractDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ContractDto>(utf8Json, s_options);
        }
        catch (JsonException)
        {
            errors.Add(new ValidationError("", ValidationErrorCode.InvalidJson, "Contract JSON is structurally invalid."));
            return null;
        }

        if (dto is null || dto.Kind != "binary")
        {
            errors.Add(new ValidationError("", ValidationErrorCode.InvalidKind, "Contract must have kind 'binary'."));
            return null;
        }

        return MapToNodes(dto);
    }
}
```

### Pattern 2: Phased Validation Pipeline
**What:** Validation runs in 4 ordered phases, each collecting errors into the same `ErrorCollector`. Later phases may skip if earlier phases produced fatal errors (e.g., unparseable JSON makes graph validation impossible).
**When to use:** Always during TryLoad/Load.
**Example:**
```csharp
// Phase 1: Parse JSON -> DTO (in BinaryContractLoader)
// Phase 2: Resolve types + sizes (check all fields have size, types are valid)
// Phase 3: Validate graph (cycle detection, single root, single child)
// Phase 4: Type-specific rules (bit overlap, array count refs, enum values)

internal static class BinaryContractValidator
{
    internal static bool Validate(BinaryContractNode root, Dictionary<string, BinaryContractNode> fields, ErrorCollector errors)
    {
        // Phase 2: type/size resolution
        ValidateTypesAndSizes(fields, errors);

        // Phase 3: graph validation (can proceed even with size errors)
        ValidateGraph(fields, errors);

        // Phase 4: type-specific (only if graph is valid)
        if (!errors.HasErrors) // or track phase-specific error flags
            ValidateTypeSpecificRules(fields, errors);

        return !errors.HasErrors;
    }
}
```

### Pattern 3: Dependency Chain Resolution (Topological Sort)
**What:** Walk the `dependsOn` chain from root to leaves, building a flat array in parse order. Compute absolute byte offset for each field as `parent.Offset + parent.Size`.
**When to use:** After validation passes.
**Example:**
```csharp
internal static class BinaryChainResolver
{
    internal static BinaryContractNode[] Resolve(
        Dictionary<string, BinaryContractNode> fields,
        string rootFieldName,
        string contractEndianness)
    {
        var ordered = new List<BinaryContractNode>();
        var current = rootFieldName;
        int offset = 0;

        while (current is not null)
        {
            var node = fields[current];
            node.AbsoluteOffset = offset;
            node.ResolvedEndianness = node.Endianness ?? contractEndianness ?? "little";
            offset += ComputeSize(node);
            ordered.Add(node);
            current = FindChild(fields, current); // field whose dependsOn == current
        }

        return ordered.ToArray();
    }
}
```

### Pattern 4: Single Sealed Class with Nullable Fields
**What:** `BinaryContractNode` is a single sealed class with nullable properties for type-specific data, mirroring `SchemaNode`.
**When to use:** Always -- this is a locked decision.
**Example:**
```csharp
internal sealed class BinaryContractNode
{
    // Identity
    internal string Name { get; init; }
    internal string? DependsOn { get; init; }

    // Type info
    internal string Type { get; init; }        // "uint8", "int16", "bits", "array", etc.
    internal int Size { get; init; }
    internal string? Encoding { get; init; }    // for string fields: "ASCII", "UTF-8"

    // Resolved at load time
    internal int AbsoluteOffset { get; set; }
    internal byte ResolvedEndianness { get; set; }  // 0 = little, 1 = big

    // Type-specific (nullable)
    internal Dictionary<string, BitFieldInfo>? BitFields { get; init; }  // for "bits" type
    internal ArrayElementInfo? ArrayElement { get; init; }               // for "array" type
    internal Dictionary<string, string>? EnumValues { get; init; }       // for "enum" type
    internal string? EnumPrimitive { get; init; }                        // for "enum" type
    internal object? Count { get; init; }                                // int or string for arrays

    // Validation rules
    internal ValidationRules? Validation { get; init; }

    // Extensions
    internal SchemaErrorInfo? ErrorInfo { get; init; }    // x-error
    internal string? XDescription { get; init; }          // x-description

    // Struct sub-fields (for array elements with type "struct")
    internal BinaryContractNode[]? StructFields { get; init; }
}
```

### Anti-Patterns to Avoid
- **Multiple node classes per type:** Do NOT create separate `BitsNode`, `ArrayNode`, `EnumNode` classes. Use single sealed class with nullable fields (locked decision).
- **Fail-fast validation:** Do NOT return on first error. Collect all errors (locked decision).
- **Absolute offsets in contract JSON:** The contract uses `dependsOn` chains, NOT absolute byte offsets. The resolver computes offsets.
- **Graph traversal at parse time:** All graph work (ordering, offset computation, endianness resolution) happens at load time. Parse time gets a flat array.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON deserialization | Custom JSON token parser for contract | `JsonSerializer.Deserialize<ContractDto>()` | Locked decision; allocations acceptable at load time; handles nested objects, arrays, string/number coercion |
| Error collection | Custom list or array for validation errors | `ErrorCollector` from Gluey.Contract core | Already built, ArrayPool-backed, has capacity limits and TooManyErrors sentinel |
| Validation error types | New error struct | `ValidationError` + `ValidationErrorCode` from core | Extend existing enum with new codes; reuse existing message pattern |
| Cycle detection | Custom graph library | Simple visited-set walk | The dependency graph is a simple linked list (each field has at most one parent via dependsOn and at most one child); DFS with visited set is sufficient |

**Key insight:** The binary contract dependency model is deliberately simple (singly-linked chain, not a DAG or tree). Cycle detection and topological sort are trivial -- a while loop with a HashSet is enough.

## Common Pitfalls

### Pitfall 1: Confusing "dependsOn direction" with parse order
**What goes wrong:** The `dependsOn` field points backward (child points to parent), but parse order goes forward (root first). Easy to walk the chain in the wrong direction.
**Why it happens:** "dependsOn" reads as "this field depends on (comes after)" but actually means "this field starts after the field it depends on."
**How to avoid:** Build a reverse map: `childOf[parentName] = childName`. Walk from root using the reverse map to get forward order.
**Warning signs:** Offset computation produces wrong values; fields appear in reverse order.

### Pitfall 2: Struct sub-fields sharing the parent scope
**What goes wrong:** Struct fields inside array elements have their own dependency chain (scoped). If the resolver tries to look them up in the top-level field dictionary, it will either fail to find them or create invalid cross-references.
**Why it happens:** The ADR specifies "Scoped chains: Struct fields inside array elements form their own independent chain."
**How to avoid:** Process struct sub-fields in a separate scope during both validation and resolution. Each struct element has its own root (no dependsOn within the struct scope) and its own offset computation relative to element start.
**Warning signs:** Struct sub-field offsets are absolute instead of relative; fields from different scopes reference each other.

### Pitfall 3: Size computation for variable-size fields (arrays)
**What goes wrong:** Arrays have computed sizes (`count * element.size` for fixed, unknown until parse time for semi-dynamic). The chain resolver needs to handle this correctly for offset computation of fields following the array.
**Why it happens:** Fixed arrays have deterministic size at load time, but semi-dynamic arrays do not.
**How to avoid:** For fixed arrays: `Size = count * element.size` (computable at load time). For semi-dynamic arrays: mark the size as dynamic; the next field's offset cannot be fully resolved at load time (it depends on runtime count). Document this distinction clearly in BinaryContractNode.
**Warning signs:** Fields after a semi-dynamic array have wrong offsets at parse time.

### Pitfall 4: Enum ValidationErrorCode is a byte
**What goes wrong:** `ValidationErrorCode` is `enum : byte`, meaning it can hold at most 256 values. Current highest value is `TooManyErrors`. Adding new binary-specific codes must stay within the byte range.
**Why it happens:** The enum was designed for compactness.
**How to avoid:** Count existing codes before adding new ones. Currently ~38 values used. Plenty of room, but add new codes just before `TooManyErrors` or in a dedicated range.
**Warning signs:** Compile error if > 255 values.

### Pitfall 5: The "kind" discriminator must be checked early
**What goes wrong:** If validation runs on a contract without `"kind": "binary"`, it may produce confusing errors about missing fields instead of a clear "wrong kind" message.
**Why it happens:** The DTO deserialization will succeed for any JSON object, even a JSON Schema.
**How to avoid:** Check `kind == "binary"` immediately after deserialization, before any field validation. Return early with a clear error.
**Warning signs:** Confusing error messages when a JSON Schema is accidentally loaded as a binary contract.

### Pitfall 6: Bit overlap validation edge cases
**What goes wrong:** Bit sub-fields like `{bit: 0, bits: 4}` and `{bit: 3, bits: 2}` overlap at bit 3. Simple range checking that only looks at start positions will miss this.
**Why it happens:** Need to check the full range `[bit, bit + bits - 1]` for each sub-field.
**How to avoid:** Use a bitmask approach: for each sub-field, set bits in a `uint` mask. If any bit is already set, it's an overlap. Also verify total doesn't exceed `container.size * 8`.
**Warning signs:** Overlapping bit fields pass validation silently.

## Code Examples

### DTO Structure for Contract Deserialization
```csharp
// Source: Based on ADR 16 contract structure
internal sealed class ContractDto
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("displayName")]
    public Dictionary<string, string>? DisplayName { get; set; }

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDto>? Fields { get; set; }
}

internal sealed class FieldDto
{
    [JsonPropertyName("dependsOn")]
    public string? DependsOn { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    [JsonPropertyName("validation")]
    public ValidationDto? Validation { get; set; }

    [JsonPropertyName("displayName")]
    public Dictionary<string, string>? DisplayName { get; set; }

    // Bit field container
    [JsonPropertyName("fields")]
    public Dictionary<string, JsonElement>? Fields { get; set; }

    // Array fields
    [JsonPropertyName("count")]
    public JsonElement? Count { get; set; }  // Can be int or string

    [JsonPropertyName("element")]
    public JsonElement? Element { get; set; }

    // Enum fields
    [JsonPropertyName("primitive")]
    public string? Primitive { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, string>? Values { get; set; }
}
```

### Cycle Detection in Dependency Chain
```csharp
// Source: Standard cycle detection for singly-linked structure
internal static bool HasCycle(Dictionary<string, BinaryContractNode> fields, ErrorCollector errors)
{
    var visited = new HashSet<string>();

    foreach (var (name, node) in fields)
    {
        if (node.DependsOn is null) continue;

        visited.Clear();
        visited.Add(name);
        var current = node.DependsOn;

        while (current is not null)
        {
            if (!visited.Add(current))
            {
                errors.Add(new ValidationError(name, ValidationErrorCode.CyclicDependency,
                    $"Cyclic dependency detected involving field '{name}'."));
                return true;
            }

            if (!fields.TryGetValue(current, out var parent))
                break; // InvalidReference handled elsewhere

            current = parent.DependsOn;
        }
    }

    return false;
}
```

### Bit Overlap Validation
```csharp
// Source: Bitmask approach for bit field overlap detection
internal static void ValidateBitFields(
    string containerName, int containerSizeBits,
    Dictionary<string, BitFieldInfo> bitFields, ErrorCollector errors)
{
    uint usedBits = 0;

    foreach (var (subName, sub) in bitFields)
    {
        int endBit = sub.Bit + sub.Bits - 1;

        if (endBit >= containerSizeBits)
        {
            errors.Add(new ValidationError($"{containerName}/{subName}",
                ValidationErrorCode.OverlappingBits,
                $"Bit field '{subName}' exceeds container size ({containerSizeBits} bits)."));
            continue;
        }

        uint mask = ((1u << sub.Bits) - 1) << sub.Bit;
        if ((usedBits & mask) != 0)
        {
            errors.Add(new ValidationError($"{containerName}/{subName}",
                ValidationErrorCode.OverlappingBits,
                $"Bit field '{subName}' overlaps with another bit field."));
        }
        usedBits |= mask;
    }
}
```

### TryLoad/Load API Surface
```csharp
// Source: Mirrors JsonContractSchema.cs exactly
public class BinaryContractSchema
{
    // Metadata properties (mirrors JsonContractSchema)
    public string? Id { get; }
    public string? Name { get; }
    public string? Version { get; }
    public Dictionary<string, string>? DisplayName { get; }

    // Resolved contract data
    internal BinaryContractNode[] OrderedFields { get; }
    internal int TotalFixedSize { get; }

    private BinaryContractSchema(/* params */) { /* ... */ }

    public static bool TryLoad(ReadOnlySpan<byte> utf8Json, out BinaryContractSchema? schema,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var root = BinaryContractLoader.Load(utf8Json, out var errors);
        if (root is null || errors.HasErrors)
        {
            schema = null;
            errors.Dispose();
            return false;
        }
        // Validate, resolve chain, construct schema
        // ...
        schema = new BinaryContractSchema(/* ... */);
        return true;
    }

    public static BinaryContractSchema? Load(ReadOnlySpan<byte> utf8Json,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        return TryLoad(utf8Json, out var schema, registry, options) ? schema : null;
    }

    // String overloads: UTF-8 encode then delegate
    public static bool TryLoad(string json, out BinaryContractSchema? schema,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return TryLoad(bytes, out schema, registry, options);
    }

    public static BinaryContractSchema? Load(string json,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return Load(bytes, registry, options);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| JsonDocument for manual parsing | JsonSerializer.Deserialize<T> for DTO mapping | .NET 6+ | Cleaner code; DTOs are testable; allocations acceptable at load time (not parse time) |
| Multiple node classes per type | Single sealed class with nullable fields | Established in SchemaNode | Consistent codebase pattern; simpler tree walking |
| Fail-fast validation | Collect-all with ErrorCollector | Established in codebase | Better developer experience; see all problems at once |

## Open Questions

1. **Semi-dynamic array size at load time**
   - What we know: Fixed arrays have deterministic size (`count * element.size`). Semi-dynamic arrays have runtime-determined size.
   - What's unclear: How does the resolver compute absolute offsets for fields that follow a semi-dynamic array? The offset depends on runtime count.
   - Recommendation: At load time, mark the semi-dynamic array's size as "dynamic." Fields after it also have dynamic offsets. At parse time, compute actual offsets by reading the count field first, then advancing. Store a flag on BinaryContractNode indicating whether offset is fixed or needs runtime computation.

2. **Enum raw-value naming convention**
   - What we know: ADR says `parsed["mode"]` returns string, `parsed["modes"]` returns raw numeric (field name + "s").
   - What's unclear: STATE.md flags this as needing a decision: "Enum raw-value naming convention (name+'s' vs '$raw' path)."
   - Recommendation: Follow the ADR's `name + "s"` convention as specified. The planner should ensure test coverage for this naming.

3. **x-prefixed field preservation**
   - What we know: x-error is used in JSON schema for error enrichment. The locked decision says to preserve x-prefixed fields.
   - What's unclear: Exact set of x-prefixed fields to support beyond x-error.
   - Recommendation: Store all `x-*` fields in a `Dictionary<string, JsonElement>?` on `BinaryContractNode` for forward compatibility. Map `x-error` specifically to `SchemaErrorInfo` as the JSON schema does.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | New `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` (Wave 0) |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests --no-build -v q` |
| Full suite command | `dotnet test --no-build -v q` (runs all test projects) |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CNTR-01 | Contract JSON loaded into BinaryContractNode tree | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractLoading" --no-build -v q` | Wave 0 |
| CNTR-02 | Dependency chain resolved into ordered field array | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ChainResolution" --no-build -v q` | Wave 0 |
| CNTR-03 | Validation: exactly one root field | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-04 | Validation: no cycles in dependency graph | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-05 | Validation: each field has at most one child | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-06 | Validation: semi-dynamic array count refs valid field | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-07 | Validation: bit sub-fields no overlap, fit container | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-08 | Validation: size declared on every field | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractValidation" --no-build -v q` | Wave 0 |
| CNTR-09 | Endianness resolved at load time | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~EndiannessResolution" --no-build -v q` | Wave 0 |
| CORE-03 | TryLoad/Load API matching JsonContractSchema | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~ContractLoading" --no-build -v q` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Binary.Tests --no-build -v q`
- **Per wave merge:** `dotnet test --no-build -v q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` -- new project file
- [ ] `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` -- new test project
- [ ] `tests/Gluey.Contract.Binary.Tests/GlobalUsings.cs` -- global using for NUnit + FluentAssertions
- [ ] Solution file updated to include both new projects
- [ ] New `ValidationErrorCode` values added to core enum (CyclicDependency, MissingRoot, OverlappingBits, MissingSize, InvalidReference, InvalidKind, SharedParent)
- [ ] New `ValidationErrorMessages` entries for the new codes

## Sources

### Primary (HIGH confidence)
- `docs/adr/16-binary-format-contract.md` -- Full contract specification, dependency chain model, all field types, validation rules
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` -- Exact API pattern to mirror
- `src/Gluey.Contract/Schema/SchemaNode.cs` -- Single-class modeling pattern
- `src/Gluey.Contract/Validation/ErrorCollector.cs` -- Error collection pattern
- `src/Gluey.Contract/Validation/ValidationErrorCode.cs` -- Existing error codes (byte enum)
- `src/Gluey.Contract/Validation/ValidationError.cs` -- Error struct to reuse
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` -- Phase 1 binary format flag already in place
- `src/Gluey.Contract/Gluey.Contract.csproj` -- InternalsVisibleTo already configured
- `.planning/codebase/CONVENTIONS.md` -- Naming and style conventions
- `.planning/codebase/ARCHITECTURE.md` -- Layer architecture and data flow

### Secondary (MEDIUM confidence)
- System.Text.Json documentation -- JsonSerializer.Deserialize<T> capabilities, JsonElement for polymorphic values (count as int or string)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in use in the codebase; no new dependencies
- Architecture: HIGH -- direct mirror of established JsonContractSchema pattern with locked decisions from CONTEXT.md
- Pitfalls: HIGH -- derived from ADR specification analysis and codebase inspection (struct scoping, semi-dynamic arrays, bit overlap edge cases)

**Research date:** 2026-03-19
**Valid until:** 2026-04-19 (stable domain, internal project)
