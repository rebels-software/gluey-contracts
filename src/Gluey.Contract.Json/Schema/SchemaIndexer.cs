// Copyright 2025 Rebels Software sp. z o.o.
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

using System.Text;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Assigns stable depth-first integer ordinals to all named properties in a <see cref="SchemaNode"/> tree.
/// Only properties from <c>properties</c> dictionaries receive ordinals; array items do not.
/// The ordinal mapping is used to size the <see cref="OffsetTable"/> during parsing.
/// Also builds pre-compiled property lookup tables for zero-allocation UTF8 property matching.
/// </summary>
internal static class SchemaIndexer
{
    /// <summary>
    /// Walks the <see cref="SchemaNode"/> tree depth-first and assigns ordinals to all named properties.
    /// Also builds PropertyLookup tables for zero-allocation property matching during parse.
    /// </summary>
    /// <param name="root">The root schema node.</param>
    /// <returns>A tuple of the name-to-ordinal dictionary (keyed by RFC 6901 path) and total property count.</returns>
    internal static (Dictionary<string, int> nameToOrdinal, int propertyCount) AssignOrdinals(SchemaNode root)
    {
        var nameToOrdinal = new Dictionary<string, int>();
        int ordinal = 0;

        WalkNode(root, nameToOrdinal, ref ordinal);

        // Second pass: build PropertyLookup tables with ordinal info
        BuildPropertyLookups(root, nameToOrdinal);

        return (nameToOrdinal, ordinal);
    }

    private static void WalkNode(SchemaNode node, Dictionary<string, int> map, ref int ordinal)
    {
        // Named properties get ordinals (depth-first: parent before children)
        if (node.Properties is not null)
        {
            foreach (var (_, child) in node.Properties)
            {
                // Assign ordinal using the child's precomputed path
                if (!map.ContainsKey(child.Path))
                {
                    map[child.Path] = ordinal++;
                }

                // Recurse into child to pick up nested properties
                WalkNode(child, map, ref ordinal);
            }
        }

        // Recurse into composition keywords (their properties also get ordinals)
        WalkArray(node.AllOf, map, ref ordinal);
        WalkArray(node.AnyOf, map, ref ordinal);
        WalkArray(node.OneOf, map, ref ordinal);

        if (node.Not is not null)
            WalkNode(node.Not, map, ref ordinal);

        if (node.If is not null)
            WalkNode(node.If, map, ref ordinal);
        if (node.Then is not null)
            WalkNode(node.Then, map, ref ordinal);
        if (node.Else is not null)
            WalkNode(node.Else, map, ref ordinal);

        // Recurse into items (but items themselves don't get ordinals)
        if (node.Items is not null)
            WalkNode(node.Items, map, ref ordinal);

        WalkArray(node.PrefixItems, map, ref ordinal);

        if (node.Contains is not null)
            WalkNode(node.Contains, map, ref ordinal);

        if (node.AdditionalProperties is not null)
            WalkNode(node.AdditionalProperties, map, ref ordinal);

        // PatternProperties children
        if (node.PatternProperties is not null)
        {
            foreach (var (_, child) in node.PatternProperties)
            {
                WalkNode(child, map, ref ordinal);
            }
        }

        // DependentSchemas children
        if (node.DependentSchemas is not null)
        {
            foreach (var (_, child) in node.DependentSchemas)
            {
                WalkNode(child, map, ref ordinal);
            }
        }

        // PropertyNames
        if (node.PropertyNames is not null)
            WalkNode(node.PropertyNames, map, ref ordinal);

        // $defs
        if (node.Defs is not null)
        {
            foreach (var (_, child) in node.Defs)
            {
                WalkNode(child, map, ref ordinal);
            }
        }
    }

    private static void WalkArray(SchemaNode[]? nodes, Dictionary<string, int> map, ref int ordinal)
    {
        if (nodes is null) return;

        foreach (var node in nodes)
        {
            WalkNode(node, map, ref ordinal);
        }
    }

    /// <summary>
    /// Builds PropertyLookup tables on every node that has Properties.
    /// Pre-computes UTF8 name bytes, child paths, ordinals, required indices,
    /// and grandchild ordinals to avoid all allocation during parse.
    /// </summary>
    private static void BuildPropertyLookups(SchemaNode root, Dictionary<string, int> nameToOrdinal)
    {
        BuildPropertyLookupsRecursive(root, nameToOrdinal);
    }

    private static void BuildPropertyLookupsRecursive(SchemaNode node, Dictionary<string, int> nameToOrdinal)
    {
        // Pre-compute Required UTF8 bytes
        if (node.Required is not null && node.RequiredUtf8 is null)
        {
            node.RequiredUtf8 = new byte[node.Required.Length][];
            for (int r = 0; r < node.Required.Length; r++)
            {
                node.RequiredUtf8[r] = Encoding.UTF8.GetBytes(node.Required[r]);
            }
        }

        if (node.Properties is not null)
        {
            var entries = new SchemaNode.PropertyEntry[node.Properties.Count];
            int i = 0;
            foreach (var (name, child) in node.Properties)
            {
                var entry = new SchemaNode.PropertyEntry(
                    Encoding.UTF8.GetBytes(name),
                    name,
                    child,
                    child.Path);

                // Set ordinal
                if (nameToOrdinal.TryGetValue(child.Path, out int ord))
                    entry.Ordinal = ord;

                // Set required index
                if (node.Required is not null)
                {
                    for (int r = 0; r < node.Required.Length; r++)
                    {
                        if (node.Required[r] == name)
                        {
                            entry.RequiredIndex = r;
                            break;
                        }
                    }
                }

                // Pre-compute grandchild ordinals for hierarchical access
                var resolvedChild = child.ResolvedRef ?? child;
                if (resolvedChild.Properties is not null)
                {
                    var gcOrdinals = new Dictionary<string, int>();
                    foreach (var (gcName, _) in resolvedChild.Properties)
                    {
                        string gcPath = SchemaNode.BuildChildPath(child.Path, gcName);
                        if (nameToOrdinal.TryGetValue(gcPath, out int gcOrd))
                        {
                            gcOrdinals[gcName] = gcOrd;
                        }
                    }
                    if (gcOrdinals.Count > 0)
                        entry.GrandchildOrdinals = gcOrdinals;
                }

                entries[i++] = entry;
            }
            node.PropertyLookup = entries;
        }

        // Recurse into all child nodes that may have their own Properties
        if (node.Properties is not null)
        {
            foreach (var (_, child) in node.Properties)
                BuildPropertyLookupsRecursive(child, nameToOrdinal);
        }
        RecurseArray(node.AllOf, nameToOrdinal);
        RecurseArray(node.AnyOf, nameToOrdinal);
        RecurseArray(node.OneOf, nameToOrdinal);
        if (node.Not is not null) BuildPropertyLookupsRecursive(node.Not, nameToOrdinal);
        if (node.If is not null) BuildPropertyLookupsRecursive(node.If, nameToOrdinal);
        if (node.Then is not null) BuildPropertyLookupsRecursive(node.Then, nameToOrdinal);
        if (node.Else is not null) BuildPropertyLookupsRecursive(node.Else, nameToOrdinal);
        if (node.Items is not null) BuildPropertyLookupsRecursive(node.Items, nameToOrdinal);
        RecurseArray(node.PrefixItems, nameToOrdinal);
        if (node.Contains is not null) BuildPropertyLookupsRecursive(node.Contains, nameToOrdinal);
        if (node.AdditionalProperties is not null) BuildPropertyLookupsRecursive(node.AdditionalProperties, nameToOrdinal);
        if (node.PatternProperties is not null)
        {
            foreach (var (_, child) in node.PatternProperties)
                BuildPropertyLookupsRecursive(child, nameToOrdinal);
        }
        if (node.DependentSchemas is not null)
        {
            foreach (var (_, child) in node.DependentSchemas)
                BuildPropertyLookupsRecursive(child, nameToOrdinal);
        }
        if (node.PropertyNames is not null) BuildPropertyLookupsRecursive(node.PropertyNames, nameToOrdinal);
        if (node.Defs is not null)
        {
            foreach (var (_, child) in node.Defs)
                BuildPropertyLookupsRecursive(child, nameToOrdinal);
        }
    }

    private static void RecurseArray(SchemaNode[]? nodes, Dictionary<string, int> nameToOrdinal)
    {
        if (nodes is null) return;
        foreach (var node in nodes)
            BuildPropertyLookupsRecursive(node, nameToOrdinal);
    }
}
