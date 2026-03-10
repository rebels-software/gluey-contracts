---
status: diagnosed
trigger: "Top-level property indexer requires leading slash result[\"/address\"] instead of bare result[\"address\"]"
created: 2026-03-10T00:00:00Z
updated: 2026-03-10T00:00:00Z
---

## Current Focus

hypothesis: CONFIRMED - nameToOrdinal dictionary is keyed by RFC 6901 paths (e.g. "/address") but user-facing indexer does raw lookup without normalizing
test: traced full key chain from SchemaNode.BuildChildPath -> SchemaIndexer -> ParseResult._nameToOrdinal
expecting: keys will have leading slash
next_action: return diagnosis

## Symptoms

expected: result["address"]["street"] works with bare property names
actual: Must use result["/address"]["street"] because dictionary keys are RFC 6901 paths
errors: Returns ParsedProperty.Empty for bare names
reproduction: Any top-level string indexer access without leading slash
started: By design - paths always built with RFC 6901 format

## Eliminated

(none - root cause found on first hypothesis)

## Evidence

- timestamp: 2026-03-10
  checked: SchemaNode.BuildChildPath (SchemaNode.cs line 330-337)
  found: Always prepends "/" to property name. For root parent (path=""), result is "/address". For nested, "/address/street".
  implication: All SchemaNode.Path values have leading slashes

- timestamp: 2026-03-10
  checked: SchemaIndexer.AssignOrdinals (SchemaIndexer.cs line 17-25, 35-37)
  found: Uses child.Path as dictionary key -> map[child.Path] = ordinal. Keys are "/address", "/address/street" etc.
  implication: nameToOrdinal dictionary is keyed by RFC 6901 pointer paths with leading slashes

- timestamp: 2026-03-10
  checked: ParseResult string indexer (ParseResult.cs line 88-99)
  found: Does raw _nameToOrdinal.TryGetValue(name, ...) with no normalization. User passes "address", dict has "/address" -> miss.
  implication: This is the exact point where the mismatch occurs

- timestamp: 2026-03-10
  checked: ParsedProperty string indexer (ParsedProperty.cs line 108-118)
  found: Same pattern - raw lookup into _childOrdinals with no normalization. Child keys would be relative names or full paths depending on how they were built.
  implication: Same fix pattern needed for child resolution if childOrdinals also uses full paths

## Resolution

root_cause: The nameToOrdinal dictionary (built by SchemaIndexer) uses RFC 6901 JSON Pointer paths as keys (e.g. "/address", "/address/street"). The ParseResult and ParsedProperty string indexers do raw dictionary lookups without normalizing the user-supplied name, so bare names like "address" don't match the "/address" keys.

fix: (not applied - diagnosis only)
verification: (not applied - diagnosis only)
files_changed: []
