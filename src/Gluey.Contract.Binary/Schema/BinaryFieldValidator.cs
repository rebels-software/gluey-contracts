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

using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Gluey.Contract.Binary.Schema;

/// <summary>
/// Static validation helpers for binary field values against contract-defined constraints.
/// Called inline during Parse() after each field's ParsedProperty is created.
/// </summary>
internal static class BinaryFieldValidator
{
    /// <summary>
    /// Validates a numeric field value against min/max constraints.
    /// </summary>
    internal static void ValidateNumeric(
        double value, string path, ValidationRules rules, ErrorCollector errors)
    {
        if (rules.Min is not null && value < rules.Min.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MinimumExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));
        }

        if (rules.Max is not null && value > rules.Max.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MaximumExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded)));
        }
    }

    /// <summary>
    /// Validates a string field value against minLength, maxLength, and pattern constraints.
    /// </summary>
    internal static void ValidateString(
        string value, string path, ValidationRules rules, Regex? compiledPattern, ErrorCollector errors)
    {
        if (rules.MinLength is not null && value.Length < rules.MinLength.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MinLengthExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MinLengthExceeded)));
        }

        if (rules.MaxLength is not null && value.Length > rules.MaxLength.Value)
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.MaxLengthExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MaxLengthExceeded)));
        }

        if (compiledPattern is not null && !compiledPattern.IsMatch(value))
        {
            errors.Add(new ValidationError(
                path,
                ValidationErrorCode.PatternMismatch,
                ValidationErrorMessages.Get(ValidationErrorCode.PatternMismatch)));
        }
    }

    /// <summary>
    /// Extracts the numeric value from a ParsedProperty as a double, dispatching by field type.
    /// Uses GetInt64() for signed integers (accepts Int8/Int16/Int32 field types).
    /// Uses GetDouble() for floats (accepts Float32/Float64 field types).
    /// </summary>
    internal static double ExtractNumericAsDouble(ParsedProperty prop, byte fieldType)
    {
        return fieldType switch
        {
            FieldTypes.UInt8 => prop.GetUInt8(),
            FieldTypes.UInt16 => prop.GetUInt16(),
            FieldTypes.UInt32 => prop.GetUInt32(),
            FieldTypes.Int8 or FieldTypes.Int16 or FieldTypes.Int32 => prop.GetInt64(),
            FieldTypes.Float32 or FieldTypes.Float64 => prop.GetDouble(),
            _ => 0
        };
    }
}
