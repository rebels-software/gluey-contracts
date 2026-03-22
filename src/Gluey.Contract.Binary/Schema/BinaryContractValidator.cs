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
/// Validates a loaded binary contract field dictionary.
/// Runs validation in phases: (1) types/sizes, (2) graph, (3) type-specific rules.
/// Collects all errors (does not fail-fast).
/// </summary>
internal static class BinaryContractValidator
{
    // Known field types that are valid in a binary contract.
    private static readonly HashSet<string> s_knownTypes = new(StringComparer.Ordinal)
    {
        "uint8", "uint16", "uint32",
        "int8", "int16", "int32",
        "float32", "float64",
        "boolean", "string", "enum",
        "bits", "array", "struct", "padding",
    };

    // Numeric types that can be used as array count references.
    private static readonly HashSet<string> s_numericTypes = new(StringComparer.Ordinal)
    {
        "uint8", "uint16", "uint32",
        "int8", "int16", "int32",
    };

    /// <summary>
    /// Validates a loaded binary contract field dictionary.
    /// Returns <c>true</c> if the contract is valid; <c>false</c> if errors were found.
    /// </summary>
    internal static bool Validate(
        Dictionary<string, BinaryContractNode> fields,
        ErrorCollector errors)
    {
        // Phase 1: types and sizes
        ValidateTypesAndSizes(fields, errors);

        // Phase 2: graph structure (root, cycles, shared parent, references)
        ValidateGraph(fields, errors);

        // Phase 3: type-specific rules (bit overlap, array count references)
        ValidateTypeSpecificRules(fields, errors);

        return !errors.HasErrors;
    }

    // -- Phase 1: Type and size validation --

    private static void ValidateTypesAndSizes(
        Dictionary<string, BinaryContractNode> fields,
        ErrorCollector errors)
    {
        foreach (var (name, node) in fields)
        {
            // Array fields compute size from count * element.size, not an explicit size declaration.
            if (node.Type == "array")
                continue;

            if (node.Size <= 0)
            {
                errors.Add(new ValidationError(
                    name,
                    ValidationErrorCode.MissingSize,
                    $"Field '{name}' is missing a required size declaration."));
            }
        }
    }

    // -- Phase 2: Graph validation --

    private static void ValidateGraph(
        Dictionary<string, BinaryContractNode> fields,
        ErrorCollector errors)
    {
        // Count root fields (DependsOn is null)
        int rootCount = 0;
        foreach (var (_, node) in fields)
        {
            if (node.DependsOn is null)
                rootCount++;
        }

        if (rootCount != 1)
        {
            errors.Add(new ValidationError(
                string.Empty,
                ValidationErrorCode.MissingRoot,
                $"Expected exactly one root field, found {rootCount}."));
        }

        // Validate dependsOn references exist
        foreach (var (name, node) in fields)
        {
            if (node.DependsOn is not null && !fields.ContainsKey(node.DependsOn))
            {
                errors.Add(new ValidationError(
                    name,
                    ValidationErrorCode.InvalidReference,
                    $"Field '{name}' depends on non-existent field '{node.DependsOn}'."));
            }
        }

        // Check shared parents (multiple fields depending on the same parent)
        var parentChildren = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, node) in fields)
        {
            if (node.DependsOn is null)
                continue;

            if (!parentChildren.TryGetValue(node.DependsOn, out var children))
            {
                children = new List<string>(2);
                parentChildren[node.DependsOn] = children;
            }

            children.Add(name);
        }

        foreach (var (parentName, children) in parentChildren)
        {
            if (children.Count > 1)
            {
                // Report error for each extra child beyond the first
                for (int i = 1; i < children.Count; i++)
                {
                    errors.Add(new ValidationError(
                        children[i],
                        ValidationErrorCode.SharedParent,
                        $"Field '{children[i]}' shares parent '{parentName}' with another field."));
                }
            }
        }

        // Cycle detection: for each field, walk the dependsOn chain
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, node) in fields)
        {
            if (node.DependsOn is null)
                continue;

            visited.Clear();
            visited.Add(name);
            var current = node.DependsOn;

            while (current is not null)
            {
                if (!visited.Add(current))
                {
                    errors.Add(new ValidationError(
                        name,
                        ValidationErrorCode.CyclicDependency,
                        $"Cyclic dependency detected involving field '{name}'."));
                    break;
                }

                if (!fields.TryGetValue(current, out var parent))
                    break; // Invalid reference handled above

                current = parent.DependsOn;
            }
        }
    }

    // -- Phase 3: Type-specific validation --

    private static void ValidateTypeSpecificRules(
        Dictionary<string, BinaryContractNode> fields,
        ErrorCollector errors)
    {
        foreach (var (name, node) in fields)
        {
            // Bit field overlap validation
            if (node.Type == "bits" && node.BitFields is not null)
            {
                ValidateBitFields(name, node.Size * 8, node.BitFields, errors);
            }

            // Semi-dynamic array count reference validation
            if (node.Type == "array" && node.Count is string countRef)
            {
                if (!fields.TryGetValue(countRef, out var refField))
                {
                    errors.Add(new ValidationError(
                        name,
                        ValidationErrorCode.InvalidReference,
                        $"Array '{name}' count references non-existent field '{countRef}'."));
                }
                else if (!s_numericTypes.Contains(refField.Type))
                {
                    errors.Add(new ValidationError(
                        name,
                        ValidationErrorCode.InvalidReference,
                        $"Array '{name}' count references non-numeric field '{countRef}'."));
                }
            }
        }
    }

    private static void ValidateBitFields(
        string containerName,
        int containerSizeBits,
        Dictionary<string, BitFieldInfo> bitFields,
        ErrorCollector errors)
    {
        uint usedBits = 0;

        foreach (var (subName, sub) in bitFields)
        {
            int endBit = sub.Bit + sub.Bits - 1;

            if (endBit >= containerSizeBits)
            {
                errors.Add(new ValidationError(
                    $"{containerName}/{subName}",
                    ValidationErrorCode.OverlappingBits,
                    $"Bit field '{subName}' exceeds container size ({containerSizeBits} bits)."));
                continue;
            }

            uint mask = ((1u << sub.Bits) - 1) << sub.Bit;
            if ((usedBits & mask) != 0)
            {
                errors.Add(new ValidationError(
                    $"{containerName}/{subName}",
                    ValidationErrorCode.OverlappingBits,
                    $"Bit field '{subName}' overlaps with another bit field in '{containerName}'."));
            }

            usedBits |= mask;
        }
    }
}
