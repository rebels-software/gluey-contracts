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

using System.Text;
using System.Text.RegularExpressions;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 string constraint keywords:
/// minLength, maxLength, pattern. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// </summary>
internal static class StringValidator
{
    /// <summary>
    /// Counts the number of Unicode codepoints in a UTF-8 byte span.
    /// Uses <see cref="Rune.DecodeFromUtf8"/> for correct multi-byte handling.
    /// </summary>
    internal static int CountCodepoints(ReadOnlySpan<byte> utf8Bytes)
    {
        int count = 0;
        while (utf8Bytes.Length > 0)
        {
            Rune.DecodeFromUtf8(utf8Bytes, out _, out int bytesConsumed);
            utf8Bytes = utf8Bytes.Slice(bytesConsumed);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Validates the "minLength" keyword. Returns true if codepointCount &gt;= minLength.
    /// </summary>
    internal static bool ValidateMinLength(int codepointCount, int minLength, string path, ErrorCollector collector)
    {
        if (codepointCount >= minLength)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinLengthExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "maxLength" keyword. Returns true if codepointCount &lt;= maxLength.
    /// </summary>
    internal static bool ValidateMaxLength(int codepointCount, int maxLength, string path, ErrorCollector collector)
    {
        if (codepointCount <= maxLength)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxLengthExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxLengthExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "pattern" keyword using a pre-compiled <see cref="Regex"/>.
    /// Returns true if the value matches the pattern.
    /// </summary>
    internal static bool ValidatePattern(string value, Regex compiledPattern, string path, ErrorCollector collector)
    {
        if (compiledPattern.IsMatch(value))
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.PatternMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.PatternMismatch)));
        return false;
    }
}
