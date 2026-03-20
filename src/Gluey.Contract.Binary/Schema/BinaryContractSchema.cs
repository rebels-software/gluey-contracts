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

namespace Gluey.Contract.Binary.Schema;

/// <summary>
/// Schema-driven binary payload parser that validates and parses raw bytes
/// according to a binary contract definition.
/// </summary>
/// <remarks>
/// Provides a dual loading API matching <see cref="Gluey.Contract.Json.JsonContractSchema"/>:
/// <list type="bullet">
///   <item><see cref="TryLoad(ReadOnlySpan{byte}, out BinaryContractSchema?, SchemaRegistry?, SchemaOptions?)"/> -- try-pattern</item>
///   <item><see cref="Load(ReadOnlySpan{byte}, SchemaRegistry?, SchemaOptions?)"/> -- returns null on failure</item>
/// </list>
/// Both have string overloads for convenience.
/// </remarks>
public class BinaryContractSchema
{
    // -- Metadata properties (mirrors JsonContractSchema) --

    /// <summary>Contract identifier from the "id" field.</summary>
    public string? Id { get; }

    /// <summary>Contract name from the "name" field.</summary>
    public string? Name { get; }

    /// <summary>Contract version from the "version" field.</summary>
    public string? Version { get; }

    /// <summary>Localized display names from the "displayName" field.</summary>
    public Dictionary<string, string>? DisplayName { get; }

    // -- Resolved contract data (internal for Phase 3 parser) --

    /// <summary>Ordered field array with precomputed offsets and endianness.</summary>
    internal BinaryContractNode[] OrderedFields { get; }

    /// <summary>
    /// Total fixed byte size of the contract (sum of all fixed field sizes).
    /// -1 if contract has dynamic-size fields.
    /// </summary>
    internal int TotalFixedSize { get; }

    /// <summary>Field lookup by name for parsed result access.</summary>
    internal Dictionary<string, int> NameToOrdinal { get; }

    private BinaryContractSchema(
        ContractMetadata metadata,
        BinaryContractNode[] orderedFields,
        int totalFixedSize,
        Dictionary<string, int> nameToOrdinal)
    {
        Id = metadata.Id;
        Name = metadata.Name;
        Version = metadata.Version;
        DisplayName = metadata.DisplayName;
        OrderedFields = orderedFields;
        TotalFixedSize = totalFixedSize;
        NameToOrdinal = nameToOrdinal;
    }

    // -- TryLoad / Load (ReadOnlySpan<byte>) --

    /// <summary>
    /// Attempts to load a binary contract schema from raw UTF-8 bytes.
    /// </summary>
    /// <param name="utf8Json">The raw UTF-8 encoded binary contract JSON.</param>
    /// <param name="schema">
    /// When this method returns <c>true</c>, contains the loaded <see cref="BinaryContractSchema"/>.
    /// When <c>false</c>, contains <c>null</c>.
    /// </param>
    /// <param name="registry">
    /// Optional <see cref="SchemaRegistry"/> for future cross-schema reference support.
    /// </param>
    /// <param name="options">
    /// Optional <see cref="SchemaOptions"/> for configuring validation behavior.
    /// </param>
    /// <returns><c>true</c> if the schema was loaded successfully; otherwise <c>false</c>.</returns>
    public static bool TryLoad(ReadOnlySpan<byte> utf8Json, out BinaryContractSchema? schema,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var errors = new ErrorCollector();

        // Phase 1: Parse JSON -> DTO -> node tree
        var (fields, contractEndianness, metadata) = BinaryContractLoader.Load(utf8Json, errors);
        if (fields is null || errors.HasErrors)
        {
            schema = null;
            errors.Dispose();
            return false;
        }

        // Phase 2-4: Validate
        if (!BinaryContractValidator.Validate(fields, errors))
        {
            schema = null;
            errors.Dispose();
            return false;
        }

        errors.Dispose();

        // Phase 5: Resolve chain
        var orderedFields = BinaryChainResolver.Resolve(fields, contractEndianness);

        // Build name-to-ordinal map
        var nameToOrdinal = new Dictionary<string, int>(orderedFields.Length, StringComparer.Ordinal);
        for (int i = 0; i < orderedFields.Length; i++)
            nameToOrdinal[orderedFields[i].Name] = i;

        // Compute total fixed size
        int totalFixedSize = ComputeTotalFixedSize(orderedFields);

        schema = new BinaryContractSchema(metadata!, orderedFields, totalFixedSize, nameToOrdinal);
        return true;
    }

    /// <summary>
    /// Loads a binary contract schema from raw UTF-8 bytes.
    /// Returns <c>null</c> if the input is not a valid binary contract. Never throws.
    /// </summary>
    public static BinaryContractSchema? Load(ReadOnlySpan<byte> utf8Json,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        return TryLoad(utf8Json, out var schema, registry, options) ? schema : null;
    }

    // -- TryLoad / Load (string) --

    /// <summary>
    /// Attempts to load a binary contract schema from a JSON string.
    /// </summary>
    public static bool TryLoad(string json, out BinaryContractSchema? schema,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return TryLoad(bytes, out schema, registry, options);
    }

    /// <summary>
    /// Loads a binary contract schema from a JSON string.
    /// Returns <c>null</c> if the input is not a valid binary contract. Never throws.
    /// </summary>
    public static BinaryContractSchema? Load(string json,
        SchemaRegistry? registry = null, SchemaOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return Load(bytes, registry, options);
    }

    // -- Parse --

    /// <summary>
    /// Parses a binary payload against this contract schema.
    /// Returns null if the payload is shorter than the contract's fixed size.
    /// </summary>
    /// <param name="data">The binary payload to parse.</param>
    /// <returns>A ParseResult with scalar fields populated, or null if structurally invalid.</returns>
    public ParseResult? Parse(byte[] data)
    {
        if (TotalFixedSize >= 0 && data.Length < TotalFixedSize)
            return null;

        var offsetTable = new OffsetTable(OrderedFields.Length);
        var errors = new ErrorCollector();

        for (int i = 0; i < OrderedFields.Length; i++)
        {
            var node = OrderedFields[i];

            if (node.IsDynamicOffset)
                break;

            byte fieldType = GetFieldType(node.Type);
            if (fieldType == 0)
                continue; // non-scalar type, skip

            var prop = new ParsedProperty(
                data, node.AbsoluteOffset, node.Size,
                "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness, fieldType);

            offsetTable.Set(i, prop);
        }

        return new ParseResult(offsetTable, errors, NameToOrdinal);
    }

    /// <summary>
    /// Parses a binary payload against this contract schema.
    /// Returns null if the payload is shorter than the contract's fixed size.
    /// </summary>
    /// <param name="data">The binary payload span to parse.</param>
    /// <returns>A ParseResult with scalar fields populated, or null if structurally invalid.</returns>
    public ParseResult? Parse(ReadOnlySpan<byte> data)
    {
        if (TotalFixedSize >= 0 && data.Length < TotalFixedSize)
            return null;

        return Parse(data.ToArray());
    }

    // -- Private helpers --

    private static byte GetFieldType(string type) => type switch
    {
        "uint8" => FieldTypes.UInt8,
        "uint16" => FieldTypes.UInt16,
        "uint32" => FieldTypes.UInt32,
        "int8" => FieldTypes.Int8,
        "int16" => FieldTypes.Int16,
        "int32" => FieldTypes.Int32,
        "float32" => FieldTypes.Float32,
        "float64" => FieldTypes.Float64,
        "boolean" => FieldTypes.Boolean,
        _ => 0 // non-scalar: string, enum, bits, array, struct, padding
    };

    private static int ComputeTotalFixedSize(BinaryContractNode[] orderedFields)
    {
        if (orderedFields.Length == 0)
            return 0;

        // If any field has dynamic offset, or the last field follows a dynamic section, total is -1
        var last = orderedFields[^1];
        if (last.IsDynamicOffset)
            return -1;

        // For the last field, compute its size contribution
        int lastFieldSize = ComputeFieldSize(last);
        if (lastFieldSize < 0)
            return -1; // last field itself is semi-dynamic

        return last.AbsoluteOffset + lastFieldSize;
    }

    private static int ComputeFieldSize(BinaryContractNode node)
    {
        if (node.Type != "array")
            return node.Size;

        if (node.Count is int fixedCount && node.ArrayElement is not null)
            return fixedCount * node.ArrayElement.Size;

        if (node.Count is string)
            return -1;

        return node.Size;
    }
}
