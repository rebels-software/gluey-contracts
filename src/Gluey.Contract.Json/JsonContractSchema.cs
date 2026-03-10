using System.Text;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Schema-driven JSON parser that validates and indexes raw bytes in a single pass.
/// Accepts standard JSON Schema to describe expected structure.
/// </summary>
/// <remarks>
/// <para>
/// Provides a dual loading API:
/// <list type="bullet">
///   <item><see cref="TryLoad(ReadOnlySpan{byte}, out JsonContractSchema?)"/> -- returns <c>bool</c> with an <c>out</c> parameter (try-pattern)</item>
///   <item><see cref="Load(ReadOnlySpan{byte})"/> -- returns <c>JsonContractSchema?</c> and never throws</item>
/// </list>
/// Both have string overloads for convenience.
/// </para>
/// <para>
/// Provides a dual parse API:
/// <list type="bullet">
///   <item><see cref="TryParse"/> -- returns <c>bool</c> with an <c>out</c> parameter (try-pattern)</item>
///   <item><see cref="Parse"/> -- returns <c>ParseResult?</c> and never throws</item>
/// </list>
/// Full parse/validation logic will be implemented in Phase 9 (Single-Pass Walker).
/// </para>
/// </remarks>
public class JsonContractSchema
{
    private readonly SchemaNode _root;
    private readonly Dictionary<string, int> _nameToOrdinal;

    /// <summary>
    /// The total number of named properties in the schema tree.
    /// Used to size the <see cref="OffsetTable"/> during parsing.
    /// </summary>
    public int PropertyCount { get; }

    /// <summary>
    /// The root <see cref="SchemaNode"/> of the loaded schema tree.
    /// Internal for use by tests and future phases that need to inspect the resolved tree.
    /// </summary>
    internal SchemaNode Root => _root;

    /// <summary>
    /// Whether the <c>format</c> keyword should produce validation errors (assertion mode).
    /// When <c>false</c> (default), format is treated as an annotation only.
    /// </summary>
    internal bool AssertFormat { get; }

    private JsonContractSchema(SchemaNode root, Dictionary<string, int> nameToOrdinal, int propertyCount, bool assertFormat = false)
    {
        _root = root;
        _nameToOrdinal = nameToOrdinal;
        PropertyCount = propertyCount;
        AssertFormat = assertFormat;
    }

    // ── Static factory methods (TryLoad / Load) ──────────────────────

    /// <summary>
    /// Attempts to load a JSON Schema from raw UTF-8 bytes.
    /// </summary>
    /// <param name="utf8Json">The raw UTF-8 encoded JSON Schema document.</param>
    /// <param name="schema">
    /// When this method returns <c>true</c>, contains the loaded <see cref="JsonContractSchema"/>.
    /// When <c>false</c>, contains <c>null</c>.
    /// </param>
    /// <param name="registry">
    /// Optional <see cref="SchemaRegistry"/> for resolving cross-schema <c>$ref</c> URIs.
    /// Pass <c>null</c> (or omit) when no cross-schema references are needed.
    /// </param>
    /// <returns><c>true</c> if the schema was loaded successfully; otherwise <c>false</c>.</returns>
    public static bool TryLoad(ReadOnlySpan<byte> utf8Json, out JsonContractSchema? schema, SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var root = JsonSchemaLoader.Load(utf8Json);
        if (root is null)
        {
            schema = null;
            return false;
        }

        // Resolve $ref/$defs/$anchor references (between loader and indexer)
        if (!SchemaRefResolver.TryResolve(root, registry))
        {
            schema = null;
            return false;
        }

        var (nameToOrdinal, propertyCount) = SchemaIndexer.AssignOrdinals(root);
        schema = new JsonContractSchema(root, nameToOrdinal, propertyCount, options?.AssertFormat ?? false);
        return true;
    }

    /// <summary>
    /// Loads a JSON Schema from raw UTF-8 bytes.
    /// Returns <c>null</c> if the input is not a valid JSON Schema. Never throws.
    /// </summary>
    /// <param name="utf8Json">The raw UTF-8 encoded JSON Schema document.</param>
    /// <param name="registry">
    /// Optional <see cref="SchemaRegistry"/> for resolving cross-schema <c>$ref</c> URIs.
    /// </param>
    /// <returns>A <see cref="JsonContractSchema"/> or <c>null</c> if loading failed.</returns>
    public static JsonContractSchema? Load(ReadOnlySpan<byte> utf8Json, SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        return TryLoad(utf8Json, out var schema, registry, options) ? schema : null;
    }

    /// <summary>
    /// Attempts to load a JSON Schema from a JSON string.
    /// </summary>
    /// <param name="json">The JSON Schema document as a string.</param>
    /// <param name="schema">
    /// When this method returns <c>true</c>, contains the loaded <see cref="JsonContractSchema"/>.
    /// When <c>false</c>, contains <c>null</c>.
    /// </param>
    /// <param name="registry">
    /// Optional <see cref="SchemaRegistry"/> for resolving cross-schema <c>$ref</c> URIs.
    /// </param>
    /// <returns><c>true</c> if the schema was loaded successfully; otherwise <c>false</c>.</returns>
    public static bool TryLoad(string json, out JsonContractSchema? schema, SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return TryLoad(bytes, out schema, registry, options);
    }

    /// <summary>
    /// Loads a JSON Schema from a JSON string.
    /// Returns <c>null</c> if the input is not a valid JSON Schema. Never throws.
    /// </summary>
    /// <param name="json">The JSON Schema document as a string.</param>
    /// <param name="registry">
    /// Optional <see cref="SchemaRegistry"/> for resolving cross-schema <c>$ref</c> URIs.
    /// </param>
    /// <returns>A <see cref="JsonContractSchema"/> or <c>null</c> if loading failed.</returns>
    public static JsonContractSchema? Load(string json, SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return Load(bytes, registry, options);
    }

    // ── Instance parse methods (stubs until Phase 9) ─────────────────

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
