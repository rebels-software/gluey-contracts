# Phase 3: Schema References - Research

**Researched:** 2026-03-09
**Domain:** JSON Schema Draft 2020-12 $ref/$defs/$anchor resolution, cycle detection, schema registry
**Confidence:** HIGH

## Summary

This phase adds reference resolution to the existing SchemaNode tree built by Phase 2's JsonSchemaLoader. The core work is a post-load resolution pass (SchemaRefResolver) that walks the tree, collects $anchor declarations, and resolves all $ref strings to their target SchemaNode references. A separate SchemaRegistry class enables cross-schema $ref resolution by URI.

The JSON Schema 2020-12 spec (Section 8.2.3.1) explicitly states that $ref resolution "is safe to perform on schema load, as the process of evaluating an instance cannot change how the reference resolves." This validates the eager resolution strategy decided in CONTEXT.md. The implementation requires understanding three distinct reference forms: JSON Pointer fragments (`#/$defs/name`), plain name fragments (`#anchor-name`), and cross-schema URI references (`https://example.com/other-schema`).

**Primary recommendation:** Implement a two-pass resolver -- first pass collects all $anchor and $id declarations into lookup tables, second pass resolves all $ref nodes. Add a `ResolvedRef` property to SchemaNode. SchemaRegistry is a simple Dictionary-backed class in the core package.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Eager resolution at load time -- separate resolution pass (new SchemaRefResolver class), not integrated into JsonSchemaLoader
- New ResolvedRef field on SchemaNode -- keeps original Ref string for diagnostics
- $anchor lookups handled in the same resolution pass -- collect all $anchor declarations into a lookup table first, then resolve both JSON Pointer $refs and $anchor $refs in one traversal
- All cycle types detected: direct self-reference, mutual cycles (A->B->A), transitive cycles (A->B->C->A)
- Track visited nodes during resolution traversal (HashSet of visited paths or nodes)
- Circular $ref fails the entire schema load -- TryLoad returns false, Load returns null
- Unresolvable $ref (missing target, bad pointer) also fails the entire load
- Cross-schema $ref must resolve to a schema already registered in the SchemaRegistry
- Standalone SchemaRegistry class, separate from JsonContractSchema
- Lives in core package (Gluey.Contract) -- format-agnostic, reusable by future format drivers
- Register pre-loaded JsonContractSchema instances: registry.Add(uri, loadedSchema)
- TryLoad/Load accept registry as optional parameter (default null = no cross-schema refs)
- Draft 2020-12 behavior for $ref with sibling keywords: apply both, no merging
- $dynamicRef/$dynamicAnchor are v2 -- NOT this phase
- Remote $ref resolution (HTTP) is explicitly out of scope

### Claude's Discretion
- Exact SchemaRefResolver internal design (recursive vs iterative traversal)
- ResolvedRef field placement and constructor parameter ordering in SchemaNode
- SchemaRegistry internal storage (Dictionary, ConcurrentDictionary, etc.)
- URI normalization rules for registry lookups
- Specific ValidationErrorCode values for cycle detection and unresolved $ref errors
- How the resolver accesses SchemaNode internals (same assembly, friend access, etc.)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCHM-03 | $ref / $defs resolution at schema-load time with cycle detection | SchemaRefResolver two-pass algorithm: collect anchors/ids, then resolve all $ref nodes with HashSet cycle detection. Failures cause TryLoad to return false. |
| SCHM-04 | $anchor resolution for named reference targets | First pass collects all $anchor declarations keyed by fragment name. $ref values without leading `/` in fragment are looked up as anchors. Anchor uniqueness per resource enforced. |
| SCHM-06 | Schema registry for multi-schema $ref resolution by URI | SchemaRegistry class in Gluey.Contract with Add/TryGet API. Passed as optional parameter to TryLoad/Load. Cross-schema $ref URI prefix matched against registered URIs. |
</phase_requirements>

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Gluey.Contract/
│   ├── SchemaNode.cs           # Add ResolvedRef property
│   ├── SchemaRegistry.cs       # NEW: URI-keyed schema registry (public)
│   └── ValidationErrorCode.cs  # Add RefCycle, RefUnresolved, AnchorUnresolved, AnchorDuplicate
├── Gluey.Contract.Json/
│   ├── JsonSchemaLoader.cs     # UNCHANGED
│   ├── SchemaRefResolver.cs    # NEW: post-load $ref resolution pass
│   ├── SchemaIndexer.cs        # Runs AFTER ref resolution
│   └── JsonContractSchema.cs   # Add optional SchemaRegistry parameter to TryLoad/Load
```

### Pattern 1: Two-Pass Resolution
**What:** First pass walks entire tree collecting $anchor and $id declarations into lookup dictionaries. Second pass walks tree resolving all $ref strings to target SchemaNode references.
**When to use:** Always -- this is the core algorithm for this phase.
**Why two passes:** A $ref may forward-reference an $anchor declared later in the document. Collecting all declarations first eliminates ordering dependencies.

```csharp
// Pass 1: Collect anchors and IDs
// Dictionary<string, SchemaNode> anchorMap -- key is anchor name
// $id nodes also registered for cross-schema base URI resolution

// Pass 2: Resolve $refs
// For each node with non-null Ref:
//   1. Parse the $ref URI string
//   2. Determine if it's a JSON Pointer fragment, anchor fragment, or cross-schema URI
//   3. Look up the target node
//   4. Set node.ResolvedRef = targetNode
//   5. Track visited set for cycle detection
```

### Pattern 2: $ref URI Classification
**What:** Classify $ref values into three categories for resolution.
**When to use:** During the resolution pass for every $ref node.

```csharp
// Category 1: JSON Pointer fragment -- starts with "#/"
// Example: "#/$defs/address"
// Resolution: Walk the schema tree following the pointer segments

// Category 2: Plain name fragment (anchor) -- starts with "#" but not "#/"
// Example: "#street_address"
// Resolution: Look up in anchor map

// Category 3: Cross-schema URI -- contains scheme or no fragment prefix
// Example: "https://example.com/schemas/address" or "other-schema#/$defs/foo"
// Resolution: Look up base URI in SchemaRegistry, then resolve fragment within that schema
```

### Pattern 3: Mutable ResolvedRef on Immutable Node
**What:** SchemaNode is conceptually immutable after construction, but ResolvedRef must be set during the resolution pass which runs after construction.
**When to use:** Adding the ResolvedRef field to SchemaNode.

```csharp
// Option A: Internal setter (recommended -- simplest, enforced by access modifier)
internal SchemaNode? ResolvedRef { get; internal set; }

// The resolver sets this during the resolution pass.
// After resolution completes, nothing else modifies it.
// "internal" access is sufficient since SchemaRefResolver is in the same assembly
// or a friend assembly (Gluey.Contract.Json has InternalsVisibleTo from Gluey.Contract).
```

### Pattern 4: JSON Pointer Navigation
**What:** Walk a SchemaNode tree following RFC 6901 JSON Pointer segments.
**When to use:** Resolving `#/$defs/name` style references.

```csharp
// Given pointer "#/$defs/address/properties/street"
// 1. Strip leading "#/"
// 2. Split by "/"
// 3. Unescape: "~1" -> "/", "~0" -> "~"  (order matters: ~1 first!)
// 4. Walk: root -> Defs["address"] -> Properties["street"]

// Key insight: Each segment maps to a specific SchemaNode child accessor:
//   "$defs" -> node.Defs[nextSegment]
//   "properties" -> node.Properties[nextSegment]
//   "items" -> node.Items (no segment consumed)
//   "prefixItems" -> node.PrefixItems[int.Parse(nextSegment)]
//   "allOf"/"anyOf"/"oneOf" -> node.AllOf[int.Parse(nextSegment)]
//   "additionalProperties" -> node.AdditionalProperties
//   "if"/"then"/"else"/"not" -> corresponding property
//   "contains" -> node.Contains
//   "patternProperties" -> node.PatternProperties[nextSegment]
//   "dependentSchemas" -> node.DependentSchemas[nextSegment]
//   "propertyNames" -> node.PropertyNames
```

### Anti-Patterns to Avoid
- **Merging $ref target into referring node:** Draft 2020-12 keeps them separate. The referring node has its own keywords + ResolvedRef pointing to target. Validation applies both.
- **Resolving $ref during tree construction (in the loader):** Forward references to $anchor would fail. The context decision locks this as a separate post-load pass.
- **Recursive resolution without cycle tracking:** Will stack overflow on circular references. Must use HashSet<string> of visited paths.
- **Modifying the SchemaNode constructor to require ResolvedRef:** This breaks Phase 2 loader code. Use a settable property instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URI parsing | Custom string splitting | `System.Uri` / `UriBuilder` | Edge cases with relative URIs, fragments, escaping |
| JSON Pointer unescaping | Manual string replace | Dedicated helper method with correct order (~1 before ~0 on decode) | Order-sensitive escaping is a common bug source |
| Thread-safe registry | Lock-based wrapper | `Dictionary<string, T>` (no thread safety needed) | Schema loading is not concurrent per the design; simplicity wins |

**Key insight:** The complexity in this phase is in the *algorithm* (traversal, cycle detection, pointer navigation), not in exotic libraries. Standard .NET types suffice.

## Common Pitfalls

### Pitfall 1: JSON Pointer Unescape Order
**What goes wrong:** Decoding `~0` before `~1` corrupts pointers containing `~1` (which represents `/`).
**Why it happens:** RFC 6901 Section 4 requires decoding `~1` -> `/` first, then `~0` -> `~`. Developers often reverse this.
**How to avoid:** Decode in the correct order: replace `~1` with `/` first, then `~0` with `~`. The existing `BuildChildPath` does encoding (opposite direction) correctly.
**Warning signs:** Tests with property names containing `~` or `/` fail.

### Pitfall 2: $anchor Uniqueness Per Resource
**What goes wrong:** Two subschemas in the same resource declare the same $anchor name. Spec says this is invalid.
**Why it happens:** The first pass collects anchors but doesn't check for duplicates.
**How to avoid:** During anchor collection, check if the anchor name already exists in the map. If duplicate found, fail the schema load.
**Warning signs:** Test with duplicate anchors silently uses one and ignores the other.

### Pitfall 3: $id Changes Base URI for Nested Resources
**What goes wrong:** A nested schema with $id creates a new resource boundary. $ref resolution within that nested resource should use the nested $id as base URI, not the root's.
**Why it happens:** Treating the entire document as one flat resource.
**How to avoid:** Track the current base URI during traversal. When encountering $id, push a new base URI. When leaving that scope, pop it.
**Warning signs:** Cross-schema $ref tests fail when schemas use $id internally.

### Pitfall 4: Cycle Detection Must Track Resolution Path, Not Just Visited Nodes
**What goes wrong:** A node is visited multiple times via different paths (legitimate -- e.g., two properties both $ref the same $defs entry). Using a global "visited" set would incorrectly flag this as a cycle.
**Why it happens:** Confusing "visited during this resolution chain" with "visited ever."
**How to avoid:** Use a per-resolution-chain HashSet (stack-based). When resolving a $ref, add the target to the current chain. After resolution returns, remove it. A cycle exists only when you encounter a node already in the current chain.
**Warning signs:** Schemas with multiple $refs to the same $defs entry incorrectly fail as circular.

### Pitfall 5: SchemaIndexer Must Run After Ref Resolution
**What goes wrong:** Ordinals are assigned before $ref targets are known, so referenced schemas' properties are missed in the offset table.
**Why it happens:** Current code runs indexer immediately after loader in JsonContractSchema.TryLoad.
**How to avoid:** Insert the resolver between loader and indexer in the TryLoad pipeline: Load -> Resolve -> Index.
**Warning signs:** PropertyCount is too low when schema uses $ref to compose properties.

### Pitfall 6: Empty Fragment "#" vs Root Pointer "#/"
**What goes wrong:** `"$ref": "#"` means "this schema resource's root", not "the root pointer". `"$ref": "#/"` is a JSON Pointer to the root's child with empty key.
**Why it happens:** Fragment `""` (after stripping `#`) points to the root of the current resource. Fragment `/` points to a child of root with key `""`.
**How to avoid:** Special-case empty fragment to return the current resource's root node.
**Warning signs:** `"$ref": "#"` resolves to wrong node or fails.

## Code Examples

### SchemaRegistry API
```csharp
// In Gluey.Contract namespace (core package)
public sealed class SchemaRegistry
{
    private readonly Dictionary<string, object> _schemas = new();
    // Using object to stay format-agnostic; JsonContractSchema is in the Json package.
    // Alternative: use a marker interface or generic.

    // Better approach: since the registry stores loaded schemas and the resolver
    // needs SchemaNode roots (internal), the registry could store SchemaNode directly
    // (internal API) and JsonContractSchema wraps the public surface.

    public void Add(string uri, object schema) { ... }
    public bool TryGet(string uri, out object? schema) { ... }
}
```

### SchemaRefResolver Structure
```csharp
// In Gluey.Contract.Json namespace
internal static class SchemaRefResolver
{
    /// <summary>
    /// Resolves all $ref nodes in the schema tree. Returns false if any $ref
    /// is unresolvable or if a cycle is detected.
    /// </summary>
    internal static bool TryResolve(SchemaNode root, SchemaRegistry? registry)
    {
        // Pass 1: Collect all anchors
        var anchors = new Dictionary<string, SchemaNode>();
        CollectAnchors(root, anchors);

        // Pass 2: Resolve all $refs
        return ResolveRefs(root, root, anchors, registry, new HashSet<string>());
    }
}
```

### JSON Pointer Resolution
```csharp
private static SchemaNode? ResolveJsonPointer(SchemaNode root, string pointer)
{
    // pointer is everything after "#/" e.g. "$defs/address"
    string[] segments = pointer.Split('/');
    SchemaNode current = root;

    for (int i = 0; i < segments.Length; i++)
    {
        // Unescape RFC 6901: ~1 -> /, ~0 -> ~ (order matters!)
        string segment = segments[i].Replace("~1", "/").Replace("~0", "~");

        current = NavigateSegment(current, segment, ref i, segments);
        if (current is null) return null;
    }

    return current;
}

private static SchemaNode? NavigateSegment(SchemaNode node, string segment, ref int i, string[] segments)
{
    return segment switch
    {
        "$defs" when node.Defs is not null && i + 1 < segments.Length
            => node.Defs.GetValueOrDefault(segments[++i].Replace("~1", "/").Replace("~0", "~")),
        "properties" when node.Properties is not null && i + 1 < segments.Length
            => node.Properties.GetValueOrDefault(segments[++i].Replace("~1", "/").Replace("~0", "~")),
        "items" => node.Items,
        "additionalProperties" => node.AdditionalProperties,
        "not" => node.Not,
        "if" => node.If,
        "then" => node.Then,
        "else" => node.Else,
        "contains" => node.Contains,
        "propertyNames" => node.PropertyNames,
        // Array-indexed keywords
        "allOf" when node.AllOf is not null && i + 1 < segments.Length
            && int.TryParse(segments[++i], out var idx) && idx < node.AllOf.Length
            => node.AllOf[idx],
        "anyOf" when node.AnyOf is not null && i + 1 < segments.Length
            && int.TryParse(segments[++i], out var idx2) && idx2 < node.AnyOf.Length
            => node.AnyOf[idx2],
        "oneOf" when node.OneOf is not null && i + 1 < segments.Length
            && int.TryParse(segments[++i], out var idx3) && idx3 < node.OneOf.Length
            => node.OneOf[idx3],
        "prefixItems" when node.PrefixItems is not null && i + 1 < segments.Length
            && int.TryParse(segments[++i], out var idx4) && idx4 < node.PrefixItems.Length
            => node.PrefixItems[idx4],
        "patternProperties" when node.PatternProperties is not null && i + 1 < segments.Length
            => node.PatternProperties.GetValueOrDefault(segments[++i].Replace("~1", "/").Replace("~0", "~")),
        "dependentSchemas" when node.DependentSchemas is not null && i + 1 < segments.Length
            => node.DependentSchemas.GetValueOrDefault(segments[++i].Replace("~1", "/").Replace("~0", "~")),
        _ => null,
    };
}
```

### Cycle Detection Pattern
```csharp
private static bool ResolveRef(SchemaNode node, SchemaNode root,
    Dictionary<string, SchemaNode> anchors, SchemaRegistry? registry,
    HashSet<string> resolutionChain)
{
    if (node.Ref is null) return true;

    // Parse the $ref URI
    string refValue = node.Ref;

    // Classify and resolve
    SchemaNode? target = refValue.StartsWith("#/")
        ? ResolveJsonPointer(root, refValue[2..])    // JSON Pointer
        : refValue.StartsWith("#")
            ? anchors.GetValueOrDefault(refValue[1..]) // Anchor
            : ResolveExternalRef(refValue, registry);  // Cross-schema

    if (target is null) return false; // Unresolvable

    // Cycle check: is target already in our resolution chain?
    if (!resolutionChain.Add(target.Path))
        return false; // Cycle detected

    // Recursively resolve the target's own $ref (if any)
    if (!ResolveRef(target, root, anchors, registry, resolutionChain))
        return false;

    resolutionChain.Remove(target.Path);

    node.ResolvedRef = target;
    return true;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| $ref replaces entire schema object (Draft 4-7) | $ref is an applicator; sibling keywords apply alongside (2020-12) | 2019 (Draft 2019-09) | Must NOT merge target into referring node |
| $recursiveRef/$recursiveAnchor | $dynamicRef/$dynamicAnchor | 2020-12 | v2 concern, not this phase |
| definitions (plain keyword) | $defs (with $ prefix) | 2019 (Draft 2019-09) | Already handled by Phase 2 loader |

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| Config file | Test project csproj files |
| Quick run command | `dotnet test tests/Gluey.Contract.Json.Tests --filter "Category=SchemaRef" --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCHM-03a | $ref to $defs resolves at load time | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Ref_To_Defs" -x` | No -- Wave 0 |
| SCHM-03b | Direct self-reference cycle detected | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Direct_Cycle" -x` | No -- Wave 0 |
| SCHM-03c | Mutual cycle (A->B->A) detected | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Mutual_Cycle" -x` | No -- Wave 0 |
| SCHM-03d | Transitive cycle (A->B->C->A) detected | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Transitive_Cycle" -x` | No -- Wave 0 |
| SCHM-03e | Unresolvable $ref fails load | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Unresolvable_Ref" -x` | No -- Wave 0 |
| SCHM-03f | Multiple refs to same $defs entry (non-cyclic) | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Multiple_Refs_Same_Target" -x` | No -- Wave 0 |
| SCHM-04a | $anchor creates named reference target | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Anchor_Resolution" -x` | No -- Wave 0 |
| SCHM-04b | Duplicate $anchor in same resource fails | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Duplicate_Anchor" -x` | No -- Wave 0 |
| SCHM-06a | SchemaRegistry Add/TryGet API | unit | `dotnet test tests/Gluey.Contract.Tests --filter "FullyQualifiedName~SchemaRegistryTests" -x` | No -- Wave 0 |
| SCHM-06b | Cross-schema $ref resolves via registry | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Cross_Schema_Ref" -x` | No -- Wave 0 |
| SCHM-06c | Cross-schema $ref to unregistered URI fails | unit | `dotnet test tests/Gluey.Contract.Json.Tests --filter "FullyQualifiedName~SchemaRefResolutionTests.Cross_Schema_Unregistered" -x` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Json.Tests --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Json.Tests/SchemaRefResolutionTests.cs` -- covers SCHM-03, SCHM-04, SCHM-06 (cross-schema via registry)
- [ ] `tests/Gluey.Contract.Tests/SchemaRegistryTests.cs` -- covers SCHM-06 (registry API in core package)

## Open Questions

1. **SchemaRegistry Storage Type**
   - What we know: Registry stores loaded schemas. SchemaNode is internal to Gluey.Contract. JsonContractSchema is public in Gluey.Contract.Json.
   - What's unclear: The registry is in Gluey.Contract (core) but needs to store JsonContractSchema instances (from Json package). This creates a circular dependency if the registry references the Json package.
   - Recommendation: Store SchemaNode (internal) in the registry for resolution purposes. Provide a public API on JsonContractSchema that wraps the internal registration. Or use a generic/object-typed registry with internal helpers that cast. The simplest approach: SchemaRegistry stores `object` publicly, and the resolver casts to the expected internal type internally.

2. **$id-Based Resource Boundaries**
   - What we know: A nested $id creates a new schema resource. Anchors within that resource are scoped to it.
   - What's unclear: How deeply to implement $id resource scoping for v1. Simple $ref to $defs works without $id awareness.
   - Recommendation: Implement basic $id tracking for anchor scoping but keep it simple. Most real-world schemas use $defs with JSON Pointer refs, not complex $id-based resource nesting.

## Sources

### Primary (HIGH confidence)
- [JSON Schema Core Spec 2020-12](https://json-schema.org/draft/2020-12/json-schema-core) - Sections 5, 8.2.1-8.2.4, 9.1.1, 9.2.1
- [$ref specification](https://www.learnjsonschema.com/2020-12/core/ref/) - URI forms, resolution rules
- [$anchor specification](https://www.learnjsonschema.com/2020-12/core/anchor/) - Format, uniqueness, interaction with $ref

### Secondary (MEDIUM confidence)
- [JSON Schema structuring guide](https://json-schema.org/understanding-json-schema/structuring) - Practical patterns for $ref/$defs composition

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new libraries needed; all .NET built-ins (System.Uri, Dictionary)
- Architecture: HIGH - Two-pass resolver is well-understood; spec explicitly endorses load-time resolution
- Pitfalls: HIGH - JSON Pointer escaping, cycle detection, and $id scoping are well-documented concerns
- Code integration: HIGH - Existing codebase fully analyzed; SchemaNode, JsonContractSchema, and SchemaIndexer integration points are clear

**Research date:** 2026-03-09
**Valid until:** 2026-04-09 (stable specification, not changing)
