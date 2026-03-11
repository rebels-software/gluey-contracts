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

using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 array size constraint keywords:
/// minItems, maxItems. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// </summary>
internal static class ArrayValidator
{
    /// <summary>
    /// Validates the "minItems" keyword. Returns true if itemCount &gt;= minItems.
    /// </summary>
    internal static bool ValidateMinItems(int itemCount, int minItems, string path, ErrorCollector collector)
    {
        if (itemCount >= minItems)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinItemsExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "maxItems" keyword. Returns true if itemCount &lt;= maxItems.
    /// </summary>
    internal static bool ValidateMaxItems(int itemCount, int maxItems, string path, ErrorCollector collector)
    {
        if (itemCount <= maxItems)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxItemsExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "contains" keyword with optional minContains/maxContains bounds.
    /// Default effective minimum is 1 when minContains is null.
    /// </summary>
    internal static bool ValidateContains(int matchCount, int? minContains, int? maxContains, string path, ErrorCollector collector)
    {
        int effectiveMin = minContains ?? 1;

        if (matchCount < effectiveMin)
        {
            if (minContains.HasValue)
            {
                collector.Add(new ValidationError(
                    path,
                    ValidationErrorCode.MinContainsExceeded,
                    ValidationErrorMessages.Get(ValidationErrorCode.MinContainsExceeded)));
            }
            else
            {
                collector.Add(new ValidationError(
                    path,
                    ValidationErrorCode.ContainsInvalid,
                    ValidationErrorMessages.Get(ValidationErrorCode.ContainsInvalid)));
            }
            return false;
        }

        if (maxContains.HasValue && matchCount > maxContains.Value)
        {
            collector.Add(new ValidationError(
                path,
                ValidationErrorCode.MaxContainsExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MaxContainsExceeded)));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the "uniqueItems" keyword. Returns true if all array elements are unique.
    /// Uses FNV-1a hashing with stackalloc for arrays &lt;= 128 items to avoid heap allocation.
    /// Handles numeric equivalence (e.g., 1 and 1.0 are considered duplicates).
    /// </summary>
    internal static bool ValidateUniqueItems(byte[][] elementBytes, bool[] isNumber, string path, ErrorCollector collector)
    {
        int count = elementBytes.Length;
        if (count <= 1)
            return true;

        // Compute FNV-1a hashes -- stackalloc for small arrays, heap fallback for large
        Span<int> hashes = count <= 128
            ? stackalloc int[count]
            : new int[count];

        for (int i = 0; i < count; i++)
        {
            hashes[i] = Fnv1aHash(elementBytes[i]);
        }

        // O(n^2) comparison with hash pre-filter
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (hashes[i] == hashes[j])
                {
                    // Byte-exact match (same hash, verify content)
                    if (((ReadOnlySpan<byte>)elementBytes[i]).SequenceEqual(elementBytes[j]))
                    {
                        collector.Add(new ValidationError(
                            path,
                            ValidationErrorCode.UniqueItemsViolation,
                            ValidationErrorMessages.Get(ValidationErrorCode.UniqueItemsViolation)));
                        return false;
                    }
                }

                // Numeric equivalence fallback for number pairs (different bytes may be equal numbers)
                if (isNumber[i] && isNumber[j]
                    && KeywordValidator.TryNumericEqual(elementBytes[i], elementBytes[j], out bool equal)
                    && equal)
                {
                    collector.Add(new ValidationError(
                        path,
                        ValidationErrorCode.UniqueItemsViolation,
                        ValidationErrorMessages.Get(ValidationErrorCode.UniqueItemsViolation)));
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Computes an FNV-1a hash of the given byte span.
    /// </summary>
    private static int Fnv1aHash(ReadOnlySpan<byte> bytes)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
