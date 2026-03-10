# ADR 10: Pre-compiled Property Lookup Tables

## Status
Accepted

## Context
During schema-validated object walking, each property name encountered in JSON needed to be:
1. Decoded from UTF-8 bytes to a `string` (`Encoding.UTF8.GetString`) — heap allocation.
2. Looked up in `SchemaNode.Properties` dictionary — another potential allocation.
3. Used to build a child path via `SchemaNode.BuildChildPath` — string concatenation, heap allocation.

For a flat object with 5 properties, this meant 10-15 string allocations per parse call just for property name handling.

## Decision
Pre-compile property lookup tables at schema load time:

### SchemaNode.PropertyEntry
Each known property gets a `PropertyEntry` containing:
- `Utf8Name` (`byte[]`) — the property name as UTF-8 bytes for direct `Span.SequenceEqual` comparison.
- `Name` (`string`) — pre-allocated string (only used when advanced features like `dependentRequired` need it).
- `ChildPath` (`string`) — pre-computed RFC 6901 path (e.g., `"/address"`).
- `Child` (`SchemaNode`) — direct reference to the child schema node.
- `Ordinal` (`int`) — pre-resolved offset table ordinal.
- `RequiredIndex` (`int`) — index into the required-property bitset (-1 if not required).
- `GrandchildOrdinals` (`Dictionary<string, int>?`) — pre-computed ordinals for nested properties.

### SchemaNode.PropertyLookup
An array of `PropertyEntry` built by `SchemaIndexer` at schema load time. The walker iterates this array with `SequenceEqual` byte comparison — no string allocation, no dictionary lookup.

### SchemaNode.RequiredUtf8
Pre-encoded `byte[][]` of required property names for zero-allocation required-property checking of unknown properties.

### Stackalloc bitset for required tracking
Replace `HashSet<string>` for required-property tracking with `stackalloc bool[requiredCount]` indexed by `RequiredIndex`. For schemas with <= 64 required properties, this is entirely stack-allocated.

## Consequences
- **Zero string allocation for known properties** — the common case (properties defined in the schema) never calls `Encoding.UTF8.GetString`.
- **Unknown properties** — still require `GetString` if advanced features (`dependentRequired`, `patternProperties`, `propertyNames`) are active on that schema node. Most schemas don't use these, so the lazy decode path rarely triggers.
- **Schema load cost** — slightly higher (builds lookup arrays, encodes UTF-8 bytes). This is a one-time cost amortized over all parse calls.
- **Linear scan vs hash lookup** — `PropertyLookup` uses linear scan with `SequenceEqual`. For typical schemas (< 20 properties), this is faster than dictionary overhead. For very large schemas, a hash-based approach could be added later.
