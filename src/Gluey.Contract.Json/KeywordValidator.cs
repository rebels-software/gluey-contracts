using System;
using System.Text.Json;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 keywords:
/// type, enum, const. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// </summary>
internal static class KeywordValidator
{
    /// <summary>
    /// Maps a <see cref="JsonByteTokenType"/> and integer flag to the corresponding <see cref="SchemaType"/>.
    /// Integer tokens map to both <c>Integer | Number</c> per JSON Schema spec.
    /// </summary>
    internal static SchemaType MapTokenToSchemaType(JsonByteTokenType tokenType, bool isInteger)
    {
        return tokenType switch
        {
            JsonByteTokenType.Null => SchemaType.Null,
            JsonByteTokenType.True => SchemaType.Boolean,
            JsonByteTokenType.False => SchemaType.Boolean,
            JsonByteTokenType.String => SchemaType.String,
            JsonByteTokenType.StartObject => SchemaType.Object,
            JsonByteTokenType.StartArray => SchemaType.Array,
            JsonByteTokenType.Number => isInteger
                ? SchemaType.Integer | SchemaType.Number
                : SchemaType.Number,
            _ => SchemaType.None,
        };
    }

    /// <summary>
    /// Determines whether the given UTF-8 number bytes represent a mathematical integer
    /// (e.g., 42, 1.0, 1e2 are integers; 1.5 is not).
    /// Uses <see cref="Utf8JsonReader.TryGetInt64"/> for battle-tested parsing.
    /// </summary>
    internal static bool IsInteger(ReadOnlySpan<byte> numberBytes)
    {
        var reader = new Utf8JsonReader(numberBytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
            return false;

        // TryGetInt64 only handles plain integer literals (no decimal point, no exponent).
        // For mathematical integer detection (1.0, 1e2, 1.5e1), parse as decimal
        // and check if it's a whole number within Int64 range.
        if (reader.TryGetInt64(out _))
            return true;

        if (!reader.TryGetDecimal(out decimal value))
            return false;

        return value == decimal.Truncate(value)
            && value >= long.MinValue
            && value <= long.MaxValue;
    }

    /// <summary>
    /// Validates the "type" keyword. Returns true if the token type matches the expected schema type.
    /// </summary>
    internal static bool ValidateType(
        SchemaType expected,
        JsonByteTokenType tokenType,
        bool isInteger,
        string path,
        ErrorCollector collector)
    {
        SchemaType actual = MapTokenToSchemaType(tokenType, isInteger);
        if ((expected & actual) != 0)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.TypeMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.TypeMismatch)));
        return false;
    }

    /// <summary>
    /// Validates the "enum" keyword. Returns true if the token bytes match any enum value
    /// (byte-exact first, numeric decimal fallback for numbers).
    /// </summary>
    internal static bool ValidateEnum(
        byte[][] enumValues,
        ReadOnlySpan<byte> tokenBytes,
        bool tokenIsNumber,
        string path,
        ErrorCollector collector)
    {
        // Byte-exact pass
        for (int i = 0; i < enumValues.Length; i++)
        {
            if (tokenBytes.SequenceEqual(enumValues[i]))
                return true;
        }

        // Numeric fallback pass
        if (tokenIsNumber)
        {
            for (int i = 0; i < enumValues.Length; i++)
            {
                if (TryNumericEqual(tokenBytes, enumValues[i], out bool equal) && equal)
                    return true;
            }
        }

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.EnumMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.EnumMismatch)));
        return false;
    }

    /// <summary>
    /// Validates the "const" keyword. Returns true if the token bytes match the expected value
    /// (byte-exact first, numeric decimal fallback for numbers).
    /// </summary>
    internal static bool ValidateConst(
        byte[] expected,
        ReadOnlySpan<byte> tokenBytes,
        bool tokenIsNumber,
        string path,
        ErrorCollector collector)
    {
        if (tokenBytes.SequenceEqual(expected))
            return true;

        if (tokenIsNumber && TryNumericEqual(tokenBytes, expected, out bool equal) && equal)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.ConstMismatch,
            ValidationErrorMessages.Get(ValidationErrorCode.ConstMismatch)));
        return false;
    }

    // ── Object keyword validators ──────────────────────────────────────

    /// <summary>
    /// Validates the "required" keyword. Returns true if all required properties are present.
    /// Collects an error for each missing property (not fail-fast).
    /// </summary>
    internal static bool ValidateRequired(
        string[] required,
        HashSet<string> seenProperties,
        string path,
        ErrorCollector collector)
    {
        bool valid = true;
        for (int i = 0; i < required.Length; i++)
        {
            if (!seenProperties.Contains(required[i]))
            {
                collector.Add(new ValidationError(
                    SchemaNode.BuildChildPath(path, required[i]),
                    ValidationErrorCode.RequiredMissing,
                    ValidationErrorMessages.Get(ValidationErrorCode.RequiredMissing)));
                valid = false;
            }
        }
        return valid;
    }

    /// <summary>
    /// Validates whether a property is allowed by the "additionalProperties" keyword.
    /// Returns true if the property is known or additional properties are allowed.
    /// </summary>
    internal static bool ValidateAdditionalProperty(
        string propertyName,
        Dictionary<string, SchemaNode>? properties,
        SchemaNode? additionalProperties,
        string path,
        ErrorCollector collector)
    {
        // Known property — always allowed
        if (properties is not null && properties.ContainsKey(propertyName))
            return true;

        // No additionalProperties constraint — spec default allows all
        if (additionalProperties is null)
            return true;

        // Boolean schema false — reject
        if (additionalProperties.BooleanSchema == false)
        {
            collector.Add(new ValidationError(
                SchemaNode.BuildChildPath(path, propertyName),
                ValidationErrorCode.AdditionalPropertyNotAllowed,
                ValidationErrorMessages.Get(ValidationErrorCode.AdditionalPropertyNotAllowed)));
            return false;
        }

        // Boolean schema true or schema-based — allow (schema validation deferred to walker)
        return true;
    }

    /// <summary>
    /// Attempts to compare two byte spans as JSON numbers using decimal parsing.
    /// Returns true if both parse successfully; sets <paramref name="equal"/> to whether values match.
    /// </summary>
    private static bool TryNumericEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, out bool equal)
    {
        equal = false;
        var readerA = new Utf8JsonReader(a);
        var readerB = new Utf8JsonReader(b);
        if (!readerA.Read() || !readerB.Read())
            return false;
        if (!readerA.TryGetDecimal(out var valA))
            return false;
        if (!readerB.TryGetDecimal(out var valB))
            return false;
        equal = valA == valB;
        return true;
    }
}
