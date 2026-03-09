# Phase 3: Schema References - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

$ref/$defs resolution with cycle detection, $anchor support, and a schema registry for cross-schema $ref resolution. Covers SCHM-03 ($ref/$defs resolution + cycle detection), SCHM-04 ($anchor resolution), SCHM-06 (schema registry). $dynamicRef/$dynamicAnchor are v2 requirements — not this phase. Remote $ref resolution (HTTP) is explicitly out of scope.

</domain>

<decisions>
## Implementation Decisions

### $ref Resolution Strategy
- Eager resolution at load time — walk the completed tree after JsonSchemaLoader builds it, resolve all $ref nodes before returning
- Separate resolution pass (new SchemaRefResolver class), not integrated into JsonSchemaLoader — Phase 2 loader code unchanged
- New `ResolvedRef` field (internal SchemaNode?) on SchemaNode — keeps original Ref string for diagnostics
- Validation (Phase 5+) checks ResolvedRef != null to follow the reference
- $anchor lookups handled in the same resolution pass — collect all $anchor declarations into a lookup table first, then resolve both JSON Pointer $refs and $anchor $refs in one traversal

### Cycle Detection
- All cycle types detected: direct self-reference, mutual cycles (A → B → A), transitive cycles (A → B → C → A)
- Track visited nodes during resolution traversal (HashSet of visited paths or nodes)
- Circular $ref fails the entire schema load — TryLoad returns false, Load returns null
- Unresolvable $ref (missing target, bad pointer) also fails the entire load — same behavior as cycles
- Cross-schema $ref must resolve to a schema already registered in the SchemaRegistry — if URI not registered, load fails

### Schema Registry API
- Standalone SchemaRegistry class, separate from JsonContractSchema
- Lives in core package (Gluey.Contract) — format-agnostic, reusable by future format drivers (ADR 5)
- Register pre-loaded JsonContractSchema instances: registry.Add(uri, loadedSchema)
- TryLoad/Load accept registry as optional parameter (default null = no cross-schema refs)
- No new overloads needed — backward compatible

### $ref with Sibling Keywords
- Draft 2020-12 behavior: apply both $ref target keywords AND sibling keywords on the referring node
- Keep $ref node separate from target — no merging. Node has its own keywords + ResolvedRef pointing to target
- Validation (Phase 5+) applies both the target's keywords and the referring node's sibling keywords
- No contradiction validation — schema author's responsibility. Validation phases naturally report errors when incompatible keywords coexist

### Claude's Discretion
- Exact SchemaRefResolver internal design (recursive vs iterative traversal)
- ResolvedRef field placement and constructor parameter ordering in SchemaNode
- SchemaRegistry internal storage (Dictionary, ConcurrentDictionary, etc.)
- URI normalization rules for registry lookups
- Specific ValidationErrorCode values for cycle detection and unresolved $ref errors
- How the resolver accesses SchemaNode internals (same assembly, friend access, etc.)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SchemaNode.cs`: Already has Ref, Defs, Anchor, Id, DynamicRef, DynamicAnchor fields as strings — parsed by Phase 2 loader
- `JsonSchemaLoader.cs`: Recursive-descent parser that builds the SchemaNode tree — Phase 3 adds a post-load resolution pass
- `JsonContractSchema.cs`: TryLoad/Load factory methods — will gain optional SchemaRegistry parameter
- `SchemaIndexer.cs`: Already traverses the tree for ordinal assignment — similar traversal pattern for ref resolution
- `SchemaNode.BuildChildPath()`: RFC 6901 path building helper — useful for resolving JSON Pointer $refs

### Established Patterns
- Internal sealed class for SchemaNode — resolver can be internal too
- Immutable after construction — ResolvedRef must be set during a controlled build phase (constructor or init-only setter)
- ValueTextEquals with u8 literals for zero-alloc keyword matching (loader pattern)
- Dual API: TryLoad/Load mirrors TryParse/Parse — registry parameter added to both

### Integration Points
- SchemaNode gains ResolvedRef field — consumed by validation phases (5-8) when walking $ref nodes
- SchemaRegistry passed through TryLoad/Load → resolver uses it for cross-schema URI lookups
- SchemaIndexer must run after ref resolution to correctly count properties across referenced schemas
- ValidationErrorCode enum may need new values for RefCycle, RefUnresolved, AnchorUnresolved

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-schema-references*
*Context gathered: 2026-03-09*
