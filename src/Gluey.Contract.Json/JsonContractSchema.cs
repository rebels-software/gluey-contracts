using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Schema-driven JSON parser that validates and indexes raw bytes in a single pass.
/// Accepts standard JSON Schema to describe expected structure.
/// </summary>
/// <remarks>
/// Provides a dual API surface:
/// <list type="bullet">
///   <item><see cref="TryParse"/> -- returns <c>bool</c> with an <c>out</c> parameter (try-pattern)</item>
///   <item><see cref="Parse"/> -- returns <c>ParseResult?</c> and never throws</item>
/// </list>
/// Full parse/validation logic will be implemented in Phase 9 (Single-Pass Walker).
/// Currently both methods return stub values (false / null).
/// </remarks>
public class JsonContractSchema
{
    /// <summary>
    /// Attempts to parse and validate the given UTF-8 JSON data against this schema.
    /// </summary>
    /// <param name="data">The raw UTF-8 bytes to parse.</param>
    /// <param name="result">
    /// When this method returns <c>true</c>, contains the <see cref="ParseResult"/>
    /// with parsed properties and any validation errors. When <c>false</c>, contains
    /// <c>default</c>.
    /// </param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This is a stub implementation. Full single-pass parse logic will be added in Phase 9.
    /// </remarks>
    // TODO: Phase 9 -- implement single-pass walker with schema-driven validation
    public bool TryParse(ReadOnlySpan<byte> data, out ParseResult result)
    {
        result = default;
        return false;
    }

    /// <summary>
    /// Parses and validates the given UTF-8 JSON data against this schema.
    /// Returns <c>null</c> if parsing cannot be completed. Never throws.
    /// </summary>
    /// <param name="data">The raw UTF-8 bytes to parse.</param>
    /// <returns>
    /// A <see cref="ParseResult"/> containing parsed properties and validation errors,
    /// or <c>null</c> if the data could not be parsed.
    /// </returns>
    /// <remarks>
    /// This is a stub implementation. Full single-pass parse logic will be added in Phase 9.
    /// </remarks>
    // TODO: Phase 9 -- implement single-pass walker with schema-driven validation
    public ParseResult? Parse(ReadOnlySpan<byte> data)
    {
        return null;
    }
}
