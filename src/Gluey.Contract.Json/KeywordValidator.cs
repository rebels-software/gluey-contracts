using System;
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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Determines whether the given UTF-8 number bytes represent a mathematical integer
    /// (e.g., 42, 1.0, 1e2 are integers; 1.5 is not).
    /// </summary>
    internal static bool IsInteger(ReadOnlySpan<byte> numberBytes)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
