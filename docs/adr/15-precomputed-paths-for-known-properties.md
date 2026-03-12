# ADR 15: Precomputed Paths for Known Properties

## Status
Accepted

## Context
Every `ParsedProperty` and `ValidationError` carries an RFC 6901 JSON Pointer path string (e.g. `/address/street`). Building these paths during the parse walk via `SchemaNode.BuildChildPath` involves `string.Replace` for `~`/`/` escaping plus concatenation — up to 3 allocations per call. On a schema with many properties, this creates measurable GC pressure on what should be a zero-allocation hot path.

## Decision
Precompute path strings for all schema-known properties at schema load time. Accept per-parse allocation only for array element paths, which are inherently data-dependent.

## Design

### Known object properties — zero allocation
The schema tree is fully known at load time. `JsonSchemaLoader` builds RFC 6901 paths for every `SchemaNode` during deserialization. `SchemaIndexer` then compiles `PropertyEntry` records containing:
- `ChildPath`: the precomputed RFC 6901 path string
- `Ordinal`: the property's index in the `OffsetTable`
- `GrandchildOrdinals`: a precomputed map from child property names to their ordinals

During the walk, when `SchemaWalker` matches a property name via UTF-8 byte comparison against `PropertyLookup`, it reads `matchedEntry.ChildPath` directly — no `BuildChildPath` call, no `Replace`, no concatenation. The path reference is copied into `ParsedProperty`, not allocated.

### Unknown object properties — lazy allocation
Properties not present in the schema (validated via `additionalProperties`, `patternProperties`) cannot have precomputed paths. These paths are built lazily via `LazyBuildChildPath`, which defers both UTF-8 decoding and path construction until actually needed. A fast path (`TryWalkValueFastPath`) skips path allocation entirely for unknown properties with simple type-only schemas that pass validation.

### Array element paths — accepted per-parse allocation
Array element paths include a data-dependent index (`/items/0`, `/items/1`, ...) that cannot be precomputed from the schema. Each element allocates one path string via `path + "/" + elementCount`. This is the only per-parse path allocation on the hot path.

This is an accepted tradeoff because:
- Array element count is bounded by input size, not schema complexity
- Users access array elements by index (`result["/items"][0]`), not by path string
- The path is a small string (parent path + "/" + a few digit characters)
- Eliminating it would require deferring materialization into `ParsedProperty`, adding complexity for minimal gain

### Full-path lookup via ParseResult
The `nameToOrdinal` dictionary maps every precomputed path (at all depths) to its ordinal. This enables O(1) deep property access:
```csharp
// Direct access to nested property — no chaining, no allocation
var street = result["/address/street"];
```

## Consequences
- The parse hot path for object properties is fully allocation-free for path strings.
- Schema load time pays a one-time cost to build and store all path strings.
- Array element paths remain the sole per-parse path allocation, bounded by input array sizes.
- `BuildChildPath` is still called at schema load time and on error/unknown-property paths, but never on the known-property hot path.
