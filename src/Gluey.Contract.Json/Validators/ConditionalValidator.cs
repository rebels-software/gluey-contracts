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
/// Static validator methods for JSON Schema Draft 2020-12 conditional keywords:
/// if/then/else. Each method returns true on success, false on failure
/// (after pushing an error to the collector).
/// These validators receive pre-computed boolean results from the walker and apply
/// conditional logic only. The walker decides when to call each method based on
/// if-schema evaluation results and presence of then/else schemas.
/// </summary>
internal static class ConditionalValidator
{
    /// <summary>
    /// Validates the "then" branch of an if/then conditional.
    /// Called by the walker only when the if-schema passed and a then-schema exists.
    /// Returns true if the then-schema passed; otherwise pushes <see cref="ValidationErrorCode.IfThenInvalid"/>.
    /// </summary>
    internal static bool ValidateIfThen(bool thenResult, string path, ErrorCollector collector)
    {
        if (thenResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.IfThenInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfThenInvalid)));
        return false;
    }

    /// <summary>
    /// Validates the "else" branch of an if/then/else conditional.
    /// Called by the walker only when the if-schema failed and an else-schema exists.
    /// Returns true if the else-schema passed; otherwise pushes <see cref="ValidationErrorCode.IfElseInvalid"/>.
    /// </summary>
    internal static bool ValidateIfElse(bool elseResult, string path, ErrorCollector collector)
    {
        if (elseResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.IfElseInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfElseInvalid)));
        return false;
    }
}
