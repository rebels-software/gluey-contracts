using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Assigns stable depth-first integer ordinals to all named properties in a <see cref="SchemaNode"/> tree.
/// Only properties from <c>properties</c> dictionaries receive ordinals; array items do not.
/// The ordinal mapping is used to size the <see cref="OffsetTable"/> during parsing.
/// </summary>
internal static class SchemaIndexer
{
    /// <summary>
    /// Walks the <see cref="SchemaNode"/> tree depth-first and assigns ordinals to all named properties.
    /// </summary>
    /// <param name="root">The root schema node.</param>
    /// <returns>A tuple of the name-to-ordinal dictionary (keyed by RFC 6901 path) and total property count.</returns>
    internal static (Dictionary<string, int> nameToOrdinal, int propertyCount) AssignOrdinals(SchemaNode root)
    {
        var nameToOrdinal = new Dictionary<string, int>();
        int ordinal = 0;

        WalkNode(root, nameToOrdinal, ref ordinal);

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
}
