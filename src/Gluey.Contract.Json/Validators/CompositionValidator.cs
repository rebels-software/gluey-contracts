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
/// Static validator methods for JSON Schema Draft 2020-12 composition keywords:
/// allOf, anyOf, oneOf, not. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// These validators receive pre-computed boolean results and apply composition logic only.
/// </summary>
internal static class CompositionValidator
{
    /// <summary>
    /// Validates the "allOf" keyword. Returns true if passCount == totalCount
    /// (all subschemas passed).
    /// </summary>
    internal static bool ValidateAllOf(int passCount, int totalCount, string path, ErrorCollector collector)
    {
        if (passCount == totalCount)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.AllOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.AllOfInvalid)));
        return false;
    }

    /// <summary>
    /// Validates the "anyOf" keyword. Returns true if passCount &gt; 0
    /// (at least one subschema passed).
    /// </summary>
    internal static bool ValidateAnyOf(int passCount, string path, ErrorCollector collector)
    {
        if (passCount > 0)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.AnyOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.AnyOfInvalid)));
        return false;
    }

    /// <summary>
    /// Validates the "oneOf" keyword. Returns true if passCount == 1
    /// (exactly one subschema passed). Both zero and multiple matches produce the same error code.
    /// </summary>
    internal static bool ValidateOneOf(int passCount, string path, ErrorCollector collector)
    {
        if (passCount == 1)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.OneOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.OneOfInvalid)));
        return false;
    }

    /// <summary>
    /// Validates the "not" keyword. Returns true if the subschema failed
    /// (!subschemaResult), meaning the value correctly does not match.
    /// </summary>
    internal static bool ValidateNot(bool subschemaResult, string path, ErrorCollector collector)
    {
        if (!subschemaResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.NotInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.NotInvalid)));
        return false;
    }
}
