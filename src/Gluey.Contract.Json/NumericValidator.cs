using System.Text.Json;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 numeric constraint keywords:
/// minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf.
/// Each method returns true on success, false on failure (after pushing error to the collector).
/// </summary>
internal static class NumericValidator
{
    /// <summary>
    /// Parses UTF-8 JSON number bytes into a <see cref="decimal"/> value.
    /// Returns false if the bytes are not a valid JSON number or exceed decimal range.
    /// </summary>
    internal static bool TryParseDecimal(ReadOnlySpan<byte> numberBytes, out decimal value)
    {
        value = 0m;
        var reader = new Utf8JsonReader(numberBytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
            return false;

        return reader.TryGetDecimal(out value);
    }

    /// <summary>
    /// Validates the "minimum" keyword (inclusive). Returns true if value &gt;= minimum.
    /// </summary>
    internal static bool ValidateMinimum(decimal value, decimal minimum, string path, ErrorCollector collector)
    {
        if (value >= minimum)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinimumExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "maximum" keyword (inclusive). Returns true if value &lt;= maximum.
    /// </summary>
    internal static bool ValidateMaximum(decimal value, decimal maximum, string path, ErrorCollector collector)
    {
        if (value <= maximum)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaximumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "exclusiveMinimum" keyword. Returns true if value &gt; exclusiveMinimum.
    /// </summary>
    internal static bool ValidateExclusiveMinimum(decimal value, decimal exclusiveMinimum, string path, ErrorCollector collector)
    {
        if (value > exclusiveMinimum)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.ExclusiveMinimumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.ExclusiveMinimumExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "exclusiveMaximum" keyword. Returns true if value &lt; exclusiveMaximum.
    /// </summary>
    internal static bool ValidateExclusiveMaximum(decimal value, decimal exclusiveMaximum, string path, ErrorCollector collector)
    {
        if (value < exclusiveMaximum)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.ExclusiveMaximumExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.ExclusiveMaximumExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "multipleOf" keyword. Returns true if value is a multiple of the divisor.
    /// Guards against division by zero (returns true if multipleOf is 0).
    /// </summary>
    internal static bool ValidateMultipleOf(decimal value, decimal multipleOf, string path, ErrorCollector collector)
    {
        if (multipleOf == 0m)
            return true;

        if (value % multipleOf == 0m)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MultipleOfInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.MultipleOfInvalid)));
        return false;
    }
}
