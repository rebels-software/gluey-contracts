# Invariant 3: Paths Precomputed from Schema

## Rule
JSON Pointer paths for every property are computed when the schema is loaded, not during parsing.

## Rationale
Path computation during parsing would require string concatenation — an allocation. Since the schema is known ahead of time, all possible paths can be precomputed and stored as static strings.

## Verification
- Schema loading builds a tree of `SchemaNode` objects with precomputed `Path` strings.
- Parser assigns paths from the schema node, never constructs paths at runtime.
- No string interpolation or concatenation in the parse path.
