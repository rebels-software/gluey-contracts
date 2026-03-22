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

namespace Gluey.Contract.Binary.Schema;

/// <summary>
/// Resolves the dependency chain into an ordered field array with precomputed
/// absolute byte offsets and resolved endianness per field.
/// </summary>
/// <remarks>
/// Expects a validated field dictionary (no cycles, exactly one root, single-child).
/// The dependency chain is walked from root to tail using a reverse map (parent -> child).
/// </remarks>
internal static class BinaryChainResolver
{
    /// <summary>
    /// Resolves the dependency chain into an ordered field array with precomputed
    /// absolute byte offsets and resolved endianness per field.
    /// </summary>
    /// <param name="fields">Validated field dictionary (from BinaryContractLoader).</param>
    /// <param name="contractEndianness">Contract-level endianness default (null, "little", or "big").</param>
    /// <returns>Ordered array of BinaryContractNode with AbsoluteOffset and ResolvedEndianness set.</returns>
    internal static BinaryContractNode[] Resolve(
        Dictionary<string, BinaryContractNode> fields,
        string? contractEndianness)
    {
        if (fields.Count == 0)
            return [];

        // Find root field (DependsOn is null)
        string? rootName = null;
        foreach (var (name, node) in fields)
        {
            if (node.DependsOn is null)
            {
                rootName = name;
                break;
            }
        }

        if (rootName is null)
            return [];

        // Build reverse map: parent -> child
        var childOf = new Dictionary<string, string>(fields.Count, StringComparer.Ordinal);
        foreach (var (name, node) in fields)
        {
            if (node.DependsOn is not null)
                childOf[node.DependsOn] = name;
        }

        // Walk from root using reverse map
        var ordered = new List<BinaryContractNode>(fields.Count);
        string? current = rootName;
        int offset = 0;
        bool dynamicMode = false;

        while (current is not null)
        {
            var node = fields[current];
            node.AbsoluteOffset = offset;
            node.IsDynamicOffset = dynamicMode;
            node.ResolvedEndianness = ResolveEndianness(node.Endianness, contractEndianness);

            // Resolve struct sub-fields if present
            ResolveStructSubFields(node, contractEndianness);

            int fieldSize = ComputeFieldSize(node);
            if (fieldSize < 0)
                dynamicMode = true; // semi-dynamic array
            else if (!dynamicMode)
                offset += fieldSize;

            ordered.Add(node);
            childOf.TryGetValue(current, out current);
        }

        return ordered.ToArray();
    }

    /// <summary>
    /// Resolves endianness for a single field: field override > contract default > "little" fallback.
    /// </summary>
    private static byte ResolveEndianness(string? fieldEndianness, string? contractEndianness)
    {
        var resolved = fieldEndianness ?? contractEndianness ?? "little";
        return resolved == "big" ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Computes the byte size of a field at load time.
    /// Returns -1 for semi-dynamic arrays (count is a string reference).
    /// </summary>
    private static int ComputeFieldSize(BinaryContractNode node)
    {
        if (node.Type != "array")
            return node.Size;

        // Array: check count type
        if (node.Count is int fixedCount && node.ArrayElement is not null)
            return fixedCount * node.ArrayElement.Size;

        if (node.Count is string)
            return -1; // semi-dynamic

        return node.Size;
    }

    /// <summary>
    /// Resolves struct sub-fields within an array element, assigning relative offsets
    /// and resolved endianness.
    /// </summary>
    private static void ResolveStructSubFields(BinaryContractNode node, string? contractEndianness)
    {
        var structFields = node.ArrayElement?.StructFields;
        if (structFields is null || structFields.Length == 0)
            return;

        // Build a dictionary for struct sub-field chain resolution
        var subFieldDict = new Dictionary<string, BinaryContractNode>(structFields.Length, StringComparer.Ordinal);
        foreach (var sf in structFields)
            subFieldDict[sf.Name] = sf;

        // Find struct root (DependsOn is null)
        string? structRoot = null;
        foreach (var sf in structFields)
        {
            if (sf.DependsOn is null)
            {
                structRoot = sf.Name;
                break;
            }
        }

        if (structRoot is null)
            return;

        // Build reverse map for struct fields
        var childOf = new Dictionary<string, string>(structFields.Length, StringComparer.Ordinal);
        foreach (var sf in structFields)
        {
            if (sf.DependsOn is not null)
                childOf[sf.DependsOn] = sf.Name;
        }

        // Walk struct chain assigning relative offsets
        string? current = structRoot;
        int relativeOffset = 0;

        while (current is not null)
        {
            var sf = subFieldDict[current];
            sf.AbsoluteOffset = relativeOffset; // relative to element start
            sf.ResolvedEndianness = ResolveEndianness(sf.Endianness, contractEndianness);
            relativeOffset += sf.Size;
            childOf.TryGetValue(current, out current);
        }
    }
}
