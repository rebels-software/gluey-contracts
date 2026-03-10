# ADR 11: Lazy String Decoding for Property Names

## Status
Accepted

## Context
The schema walker encounters property names as UTF-8 byte spans from the JSON tokenizer. Many downstream operations need either a `string` name, a child path string, or both — but not all properties trigger all operations. Eagerly decoding every property name to a string wastes allocation on properties that only need byte-level comparison.

The walker must handle two categories of properties differently:

- **Known properties** (defined in `SchemaNode.Properties`) — resolved via pre-compiled `PropertyLookup` using UTF-8 byte comparison. Name, path, and ordinal are pre-computed at schema load time.
- **Unknown properties** (not in schema, subject to `additionalProperties`) — may or may not need a string name depending on which schema features are active.

## Decision
Defer `Encoding.UTF8.GetString()` as long as possible. The walker declares `string? name = null; string? childPath = null;` and only populates them when a downstream operation requires it.

### String is decoded only when:
1. **`seenPropertyNames` HashSet is active** — only allocated when `dependentRequired`, `dependentSchemas`, `propertyNames`, or composition/conditional keywords are present on the schema node.
2. **`additionalProperties` is `false`** — needs the name for the error message.
3. **`patternProperties` is present** — needs the name for regex matching.
4. **Error reporting** — needs path for `ValidationError`.
5. **OffsetTable storage** — needs path for `ParsedProperty` construction.

### String is never decoded when:
- Property is known (uses `PropertyEntry.Name` and `PropertyEntry.ChildPath` — pre-allocated at schema load).
- Unknown property passes fast-path validation (see ADR 12).
- No advanced schema features are active on the node.

### HashSet allocation is conditional
`HashSet<string> seenPropertyNames` is only created when composition, conditional, dependency, or propertyNames keywords are present on the schema node. Most schemas don't use these, so the common case avoids the HashSet entirely.

## Consequences
- **Zero string allocation in the common case** — flat schemas with known properties and simple `additionalProperties` never call `GetString`.
- **Code complexity** — the walker uses `name ??= ...` and `childPath ??= ...` patterns throughout, making the control flow harder to follow. This is an acceptable tradeoff for zero allocation.
- **Correctness risk** — forgetting to populate `name` or `childPath` before use would cause a `NullReferenceException`. The `??=` pattern and `LazyBuildChildPath` helper mitigate this.
