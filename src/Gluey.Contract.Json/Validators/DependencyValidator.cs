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
/// Static validator methods for JSON Schema Draft 2020-12 dependency keywords:
/// dependentRequired and dependentSchemas. Each method returns true on success,
/// false on failure (after pushing errors to the collector).
/// </summary>
internal static class DependencyValidator
{
    /// <summary>
    /// Validates the "dependentRequired" keyword. For each trigger property present
    /// in <paramref name="presentProperties"/>, checks that all dependent property names
    /// are also present. Collects all missing property errors (not fail-fast).
    /// Errors use the root <paramref name="path"/> directly (not BuildChildPath).
    /// </summary>
    internal static bool ValidateDependentRequired(
        Dictionary<string, string[]> dependentRequired,
        HashSet<string> presentProperties,
        string path,
        ErrorCollector collector)
    {
        bool valid = true;

        foreach (var entry in dependentRequired)
        {
            if (!presentProperties.Contains(entry.Key))
                continue;

            string[] required = entry.Value;
            for (int i = 0; i < required.Length; i++)
            {
                if (!presentProperties.Contains(required[i]))
                {
                    collector.Add(new ValidationError(
                        path,
                        ValidationErrorCode.DependentRequiredMissing,
                        ValidationErrorMessages.Get(ValidationErrorCode.DependentRequiredMissing)));
                    valid = false;
                }
            }
        }

        return valid;
    }

    /// <summary>
    /// Validates a single dependent schema result. Called by the walker once per
    /// trigger property whose dependent schema was evaluated.
    /// Returns true if the schema passed; otherwise pushes <see cref="ValidationErrorCode.DependentSchemaInvalid"/>.
    /// </summary>
    internal static bool ValidateDependentSchema(bool schemaResult, string path, ErrorCollector collector)
    {
        if (schemaResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.DependentSchemaInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.DependentSchemaInvalid)));
        return false;
    }
}
