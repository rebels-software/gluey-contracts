// Copyright 2026 Rebels Software sp. z o.o.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Two-pass reference resolver for JSON Schema Draft 2020-12.
/// Pass 1: Collects all <c>$anchor</c> declarations into a lookup table.
/// Pass 2: Resolves all <c>$ref</c> values to their target <see cref="SchemaNode"/>
/// and sets <see cref="SchemaNode.ResolvedRef"/>.
/// </summary>
internal static class SchemaRefResolver
{
    /// <summary>
    /// Resolves all <c>$ref</c> references in the schema tree rooted at <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The root schema node.</param>
    /// <param name="registry">Optional registry for cross-schema <c>$ref</c> resolution.</param>
    /// <returns><c>true</c> if all references resolved successfully; <c>false</c> if a cycle,
    /// duplicate anchor, or unresolvable reference was detected.</returns>
    internal static bool TryResolve(SchemaNode root, SchemaRegistry? registry)
    {
        // Pass 1: Collect all $anchor declarations
        var anchors = new Dictionary<string, SchemaNode>(StringComparer.Ordinal);
        if (!CollectAnchors(root, anchors))
            return false;

        // Pass 2: Resolve all $ref values
        return ResolveRefs(root, root, anchors, registry);
    }

    // ── Pass 1: Anchor Collection ────────────────────────────────────────

    private static bool CollectAnchors(SchemaNode node, Dictionary<string, SchemaNode> anchors)
    {
        if (node.Anchor is not null)
        {
            // Duplicate $anchor in the same resource fails the load
            if (!anchors.TryAdd(node.Anchor, node))
                return false;
        }

        return WalkChildren(node, static (child, state) => CollectAnchors(child, state), anchors);
    }

    // ── Pass 2: Ref Resolution ───────────────────────────────────────────

    private static bool ResolveRefs(
        SchemaNode node,
        SchemaNode root,
        Dictionary<string, SchemaNode> anchors,
        SchemaRegistry? registry)
    {
        if (node.Ref is not null)
        {
            var target = ResolveRefValue(node.Ref, root, anchors, registry);
            if (target is null)
                return false;

            // Per-chain cycle detection: follow the $ref chain from target
            // and detect if we arrive back at a node already in the chain.
            if (!CheckRefChainForCycles(node, target))
                return false;

            node.ResolvedRef = target;
        }

        return WalkChildren(node, static (child, state) =>
            ResolveRefs(child, state.root, state.anchors, state.registry),
            (root, anchors, registry));
    }

    /// <summary>
    /// Checks that following the $ref chain from <paramref name="target"/> does not
    /// lead back to <paramref name="sourceNode"/>, forming a cycle.
    /// Uses a per-chain <see cref="HashSet{T}"/> tracking node paths.
    /// </summary>
    private static bool CheckRefChainForCycles(SchemaNode sourceNode, SchemaNode target)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { sourceNode.Path };

        var current = target;
        while (current is not null)
        {
            if (!visited.Add(current.Path))
                return false; // Cycle detected

            // Follow the chain: if current has a $ref that was already resolved, keep going
            current = current.ResolvedRef;
        }

        return true;
    }

    // ── Ref Value Resolution ─────────────────────────────────────────────

    private static SchemaNode? ResolveRefValue(
        string refValue,
        SchemaNode root,
        Dictionary<string, SchemaNode> anchors,
        SchemaRegistry? registry)
    {
        if (refValue == "#")
        {
            // Empty fragment: resolve to schema root
            return root;
        }

        if (refValue.StartsWith("#/", StringComparison.Ordinal))
        {
            // JSON Pointer within current schema
            return NavigateJsonPointer(refValue.AsSpan(2), root);
        }

        if (refValue.StartsWith("#", StringComparison.Ordinal))
        {
            // Anchor reference (e.g., "#my-anchor")
            var anchorName = refValue.Substring(1);
            return anchors.TryGetValue(anchorName, out var anchorTarget) ? anchorTarget : null;
        }

        // Cross-schema URI reference
        return ResolveCrossSchemaRef(refValue, registry);
    }

    /// <summary>
    /// Resolves a cross-schema <c>$ref</c> URI, optionally with a fragment.
    /// </summary>
    private static SchemaNode? ResolveCrossSchemaRef(string uri, SchemaRegistry? registry)
    {
        if (registry is null)
            return null;

        // Split at '#' to separate base URI from fragment
        var hashIndex = uri.IndexOf('#');
        if (hashIndex < 0)
        {
            // No fragment -- look up the URI as-is and return the root
            return registry.TryGet(uri, out var node) ? node : null;
        }

        var baseUri = uri.Substring(0, hashIndex);
        var fragment = uri.Substring(hashIndex);

        if (!registry.TryGet(baseUri, out var remoteRoot) || remoteRoot is null)
            return null;

        if (fragment == "#")
        {
            return remoteRoot;
        }

        if (fragment.StartsWith("#/", StringComparison.Ordinal))
        {
            return NavigateJsonPointer(fragment.AsSpan(2), remoteRoot);
        }

        // Anchor reference in remote schema -- we would need to collect anchors
        // from the remote schema; for now, anchor references across schemas are
        // not supported (only JSON Pointer fragments).
        return null;
    }

    // ── JSON Pointer Navigation (RFC 6901) ───────────────────────────────

    /// <summary>
    /// Navigates a JSON Pointer (without the leading <c>#/</c>) within a schema tree.
    /// Handles container keywords ($defs, properties, etc.) that require a two-step
    /// lookup: the keyword segment selects the dictionary, the next segment selects the entry.
    /// </summary>
    private static SchemaNode? NavigateJsonPointer(ReadOnlySpan<char> pointer, SchemaNode root)
    {
        var current = root;
        var pointerStr = pointer.ToString();
        var segments = pointerStr.Split('/');

        for (int i = 0; i < segments.Length; i++)
        {
            var rawSegment = segments[i];
            if (rawSegment.Length == 0)
                continue;

            // RFC 6901 unescaping: ~1 -> /, ~0 -> ~ (order matters!)
            var segment = rawSegment.Replace("~1", "/").Replace("~0", "~");

            // Container keywords: consume this segment + the next one to look up a dictionary entry
            if (IsContainerKeyword(segment))
            {
                i++;
                if (i >= segments.Length)
                    return null; // Container keyword without a key segment

                var key = segments[i].Replace("~1", "/").Replace("~0", "~");
                current = LookupContainerEntry(current, segment, key);
            }
            else
            {
                current = NavigateSegment(current, segment);
            }

            if (current is null)
                return null;
        }

        return current;
    }

    /// <summary>
    /// Returns <c>true</c> if the segment names a container keyword whose children
    /// are stored in a dictionary (requiring the next segment as a lookup key).
    /// </summary>
    private static bool IsContainerKeyword(string segment) => segment is "$defs" or "properties"
        or "patternProperties" or "dependentSchemas";

    /// <summary>
    /// Looks up an entry in a container keyword's dictionary.
    /// </summary>
    private static SchemaNode? LookupContainerEntry(SchemaNode node, string container, string key)
    {
        return container switch
        {
            "$defs" => node.Defs is not null && node.Defs.TryGetValue(key, out var d) ? d : null,
            "properties" => node.Properties is not null && node.Properties.TryGetValue(key, out var p) ? p : null,
            "patternProperties" => node.PatternProperties is not null && node.PatternProperties.TryGetValue(key, out var pp) ? pp : null,
            "dependentSchemas" => node.DependentSchemas is not null && node.DependentSchemas.TryGetValue(key, out var ds) ? ds : null,
            _ => null,
        };
    }

    /// <summary>
    /// Navigates a single non-container JSON Pointer segment within a <see cref="SchemaNode"/>.
    /// </summary>
    private static SchemaNode? NavigateSegment(SchemaNode node, string segment)
    {
        return segment switch
        {
            "additionalProperties" => node.AdditionalProperties,
            "items" => node.Items,
            "contains" => node.Contains,
            "not" => node.Not,
            "if" => node.If,
            "then" => node.Then,
            "else" => node.Else,
            "propertyNames" => node.PropertyNames,
            _ => NavigateIndexed(node, segment),
        };
    }

    /// <summary>
    /// Navigates an array-indexed segment (e.g., "0", "1") for allOf/anyOf/oneOf/prefixItems.
    /// </summary>
    private static SchemaNode? NavigateIndexed(SchemaNode node, string segment)
    {
        if (!int.TryParse(segment, out var index) || index < 0)
            return null;

        if (node.AllOf is not null && index < node.AllOf.Length)
            return node.AllOf[index];
        if (node.AnyOf is not null && index < node.AnyOf.Length)
            return node.AnyOf[index];
        if (node.OneOf is not null && index < node.OneOf.Length)
            return node.OneOf[index];
        if (node.PrefixItems is not null && index < node.PrefixItems.Length)
            return node.PrefixItems[index];

        return null;
    }

    // ── Tree Walk Helper ─────────────────────────────────────────────────

    /// <summary>
    /// Walks all children of a <see cref="SchemaNode"/>, invoking <paramref name="visitor"/>
    /// on each. Returns <c>false</c> on the first <c>false</c> return from the visitor.
    /// </summary>
    private static bool WalkChildren<TState>(
        SchemaNode node,
        Func<SchemaNode, TState, bool> visitor,
        TState state)
    {
        // Properties
        if (node.Properties is not null)
        {
            foreach (var (_, child) in node.Properties)
            {
                if (!visitor(child, state))
                    return false;
            }
        }

        // $defs
        if (node.Defs is not null)
        {
            foreach (var (_, child) in node.Defs)
            {
                if (!visitor(child, state))
                    return false;
            }
        }

        // Composition
        if (!WalkArray(node.AllOf, visitor, state)) return false;
        if (!WalkArray(node.AnyOf, visitor, state)) return false;
        if (!WalkArray(node.OneOf, visitor, state)) return false;

        if (node.Not is not null && !visitor(node.Not, state))
            return false;

        // Conditional
        if (node.If is not null && !visitor(node.If, state))
            return false;
        if (node.Then is not null && !visitor(node.Then, state))
            return false;
        if (node.Else is not null && !visitor(node.Else, state))
            return false;

        // Array applicators
        if (node.Items is not null && !visitor(node.Items, state))
            return false;
        if (!WalkArray(node.PrefixItems, visitor, state)) return false;
        if (node.Contains is not null && !visitor(node.Contains, state))
            return false;

        // Object applicators
        if (node.AdditionalProperties is not null && !visitor(node.AdditionalProperties, state))
            return false;

        if (node.PatternProperties is not null)
        {
            foreach (var (_, child) in node.PatternProperties)
            {
                if (!visitor(child, state))
                    return false;
            }
        }

        if (node.DependentSchemas is not null)
        {
            foreach (var (_, child) in node.DependentSchemas)
            {
                if (!visitor(child, state))
                    return false;
            }
        }

        if (node.PropertyNames is not null && !visitor(node.PropertyNames, state))
            return false;

        return true;
    }

    private static bool WalkArray<TState>(SchemaNode[]? nodes, Func<SchemaNode, TState, bool> visitor, TState state)
    {
        if (nodes is null) return true;

        foreach (var node in nodes)
        {
            if (!visitor(node, state))
                return false;
        }

        return true;
    }
}
