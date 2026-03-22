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

    /// <summary>Total OffsetTable capacity including synthetic ordinals for enum suffixes and bit sub-fields.</summary>
    internal int TotalOrdinalCapacity { get; }

    /// <summary>Total number of bit sub-fields across all containers, for scratch buffer sizing.</summary>
    private readonly int _totalBitSubFields;

    private BinaryContractSchema(
        ContractMetadata metadata,
        BinaryContractNode[] orderedFields,
        int totalFixedSize,
        Dictionary<string, int> nameToOrdinal,
        int totalOrdinalCapacity,
        int totalBitSubFields)
    {
        Id = metadata.Id;
        Name = metadata.Name;
        Version = metadata.Version;
        DisplayName = metadata.DisplayName;
        OrderedFields = orderedFields;
        TotalFixedSize = totalFixedSize;
        NameToOrdinal = nameToOrdinal;
        TotalOrdinalCapacity = totalOrdinalCapacity;
        _totalBitSubFields = totalBitSubFields;
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
    /// <param name="registry">Reserved for future cross-schema reference support.</param>
    /// <param name="options">Reserved for future validation behavior configuration.</param>
    /// <returns><c>true</c> if the schema was loaded successfully; otherwise <c>false</c>.</returns>
    public static bool TryLoad(ReadOnlySpan<byte> utf8Json, out BinaryContractSchema? schema,
        object? registry = null, object? options = null)
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

        // Build name-to-ordinal map with synthetic entries for enums and bit sub-fields
        int extraOrdinals = 0;
        int totalBitSubFields = 0;
        for (int i = 0; i < orderedFields.Length; i++)
        {
            var node = orderedFields[i];
            if (node.Type == "enum")
                extraOrdinals++; // one extra for "names" suffix entry
            if (node.Type == "bits" && node.BitFields is not null)
            {
                extraOrdinals += node.BitFields.Count; // one per sub-field
                totalBitSubFields += node.BitFields.Count;
            }
        }

        int totalCapacity = orderedFields.Length + extraOrdinals;
        var nameToOrdinal = new Dictionary<string, int>(totalCapacity, StringComparer.Ordinal);
        int nextOrdinal = orderedFields.Length; // synthetic ordinals start after base ordinals

        for (int i = 0; i < orderedFields.Length; i++)
        {
            var node = orderedFields[i];
            nameToOrdinal[node.Name] = i;

            // Enum: add suffixed entry (e.g., "modes" -> ordinal for string label)
            if (node.Type == "enum")
            {
                nameToOrdinal[node.Name + "s"] = nextOrdinal++;
            }

            // Bits: add sub-field path entries (e.g., "flags/isCharging" -> ordinal)
            if (node.Type == "bits" && node.BitFields is not null)
            {
                foreach (var subFieldName in node.BitFields.Keys)
                {
                    nameToOrdinal[node.Name + "/" + subFieldName] = nextOrdinal++;
                }
            }
        }

        // Compute total fixed size
        int totalFixedSize = ComputeTotalFixedSize(orderedFields);

        schema = new BinaryContractSchema(metadata!, orderedFields, totalFixedSize, nameToOrdinal, totalCapacity, totalBitSubFields);
        return true;
    }

    /// <summary>
    /// Loads a binary contract schema from raw UTF-8 bytes.
    /// Returns <c>null</c> if the input is not a valid binary contract. Never throws.
    /// </summary>
    public static BinaryContractSchema? Load(ReadOnlySpan<byte> utf8Json,
        object? registry = null, object? options = null)
    {
        return TryLoad(utf8Json, out var schema, registry, options) ? schema : null;
    }

    // -- TryLoad / Load (string) --

    /// <summary>
    /// Attempts to load a binary contract schema from a JSON string.
    /// </summary>
    public static bool TryLoad(string json, out BinaryContractSchema? schema,
        object? registry = null, object? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return TryLoad(bytes, out schema, registry, options);
    }

    /// <summary>
    /// Loads a binary contract schema from a JSON string.
    /// Returns <c>null</c> if the input is not a valid binary contract. Never throws.
    /// </summary>
    public static BinaryContractSchema? Load(string json,
        object? registry = null, object? options = null)
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

        // Clone NameToOrdinal for parse-local element path expansion (D-08, Pitfall 1)
        var parseNameToOrdinal = new Dictionary<string, int>(NameToOrdinal, StringComparer.Ordinal);

        // Rent ArrayBuffer for array element storage (D-03)
        var arrayBuffer = ArrayBuffer.Rent(initialCapacity: 16, maxOrdinal: 16);
        int arrayRegionIndex = 0;

        // Compute expanded OffsetTable capacity for fixed array elements + semi-dynamic headroom
        int expandedCapacity = TotalOrdinalCapacity;
        for (int i = 0; i < OrderedFields.Length; i++)
        {
            var node = OrderedFields[i];
            if (node.Type == "array" && node.ArrayElement is not null)
            {
                if (node.Count is int fixedCount)
                {
                    if (node.ArrayElement.StructFields is not null && node.ArrayElement.StructFields.Length > 0)
                        expandedCapacity += fixedCount * node.ArrayElement.StructFields.Length;
                    else
                        expandedCapacity += fixedCount;
                }
                else if (node.Count is string)
                {
                    // Semi-dynamic: add headroom (actual count unknown until parse time)
                    expandedCapacity += 64;
                }
            }
        }

        var offsetTable = new OffsetTable(expandedCapacity);
        var errors = new ErrorCollector();

        // Allocate scratch buffer for bit sub-field extracted values
        byte[]? bitScratchBuffer = _totalBitSubFields > 0 ? new byte[_totalBitSubFields] : null;
        int bitScratchOffset = 0;

        // Track next dynamic ordinal for array element entries (beyond pre-allocated range)
        int nextDynamicOrdinal = TotalOrdinalCapacity;
        int pass2StartIndex = -1;

        for (int i = 0; i < OrderedFields.Length; i++)
        {
            var node = OrderedFields[i];

            if (node.IsDynamicOffset)
            {
                pass2StartIndex = i;
                break;
            }

            byte fieldType = GetFieldType(node.Type);
            if (fieldType == 0)
            {
                // Handle array type nodes
                if (node.Type == "array" && node.ArrayElement is not null)
                {
                    int count;
                    if (node.Count is int arrayCount)
                    {
                        count = arrayCount;
                    }
                    else if (node.Count is string countFieldName)
                    {
                        // Semi-dynamic: resolve count from already-parsed field (D-06)
                        if (parseNameToOrdinal.TryGetValue(countFieldName, out int countOrdinal))
                        {
                            var countProp = offsetTable[countOrdinal];
                            count = ReadCountValue(countProp);
                        }
                        else
                        {
                            count = 0; // Count field not found -- treat as zero-count
                        }
                    }
                    else
                    {
                        continue; // no valid count
                    }

                    var elemInfo = node.ArrayElement;

                    // Graceful degradation (D-05): clamp count by available payload bytes
                    int arrayBaseOffset = node.AbsoluteOffset;
                    int elementSize = elemInfo.Size;
                    int availableBytes = data.Length - arrayBaseOffset;
                    int maxFit = elementSize > 0 ? availableBytes / elementSize : 0;
                    if (maxFit < count) count = maxFit;

                    int currentArrayRegion = arrayRegionIndex++;

                    if (elemInfo.Type == "struct" && elemInfo.StructFields is not null)
                    {
                        // Struct array elements (D-07, COMP-03)
                        for (int e = 0; e < count; e++)
                        {
                            int elementBase = arrayBaseOffset + (e * elementSize);

                            // Register sub-fields in OffsetTable + NameToOrdinal for O(1) path access
                            foreach (var sf in elemInfo.StructFields)
                            {
                                int sfOffset = elementBase + sf.AbsoluteOffset;
                                byte sfFieldType = GetFieldType(sf.Type);
                                string sfPath = node.Name + "/" + e + "/" + sf.Name;
                                var sfProp = new ParsedProperty(
                                    data, sfOffset, sf.Size, "/" + sfPath,
                                    /*format:*/ 1, sf.ResolvedEndianness, sfFieldType);
                                parseNameToOrdinal[sfPath] = nextDynamicOrdinal;
                                offsetTable.Set(nextDynamicOrdinal, sfProp);
                                nextDynamicOrdinal++;
                                if (sf.Validation is not null)
                                {
                                    byte sfFieldType2 = GetFieldType(sf.Type);
                                    if (sfFieldType2 == FieldTypes.String)
                                    {
                                        string sfStrValue = sfProp.GetString();
                                        BinaryFieldValidator.ValidateString(sfStrValue, "/" + sfPath, sf.Validation, sf.CompiledPattern, sf.ErrorInfo, errors);
                                    }
                                    else if (sfFieldType2 != 0 && sfFieldType2 != FieldTypes.Padding && sfFieldType2 != FieldTypes.Bits && sfFieldType2 != FieldTypes.Enum)
                                    {
                                        double sfNumValue = BinaryFieldValidator.ExtractNumericAsDouble(sfProp, sfFieldType2);
                                        BinaryFieldValidator.ValidateNumeric(sfNumValue, "/" + sfPath, sf.Validation, sf.ErrorInfo, errors);
                                    }
                                }
                            }

                            // Create struct element entry wired to NameToOrdinal for child navigation
                            var structElemProp = new ParsedProperty(
                                data, elementBase, elementSize, "/" + node.Name + "/" + e,
                                /*format:*/ 1, node.ResolvedEndianness, FieldTypes.None,
                                offsetTable, parseNameToOrdinal, null, -1);
                            arrayBuffer.Add(currentArrayRegion, structElemProp);
                        }
                    }
                    else
                    {
                        // Scalar array elements (D-02)
                        byte elemFieldType = GetFieldType(elemInfo.Type);
                        for (int e = 0; e < count; e++)
                        {
                            int elemOffset = arrayBaseOffset + (e * elementSize);
                            string elemPath = node.Name + "/" + e;
                            var elemProp = new ParsedProperty(
                                data, elemOffset, elementSize, "/" + elemPath,
                                /*format:*/ 1, node.ResolvedEndianness, elemFieldType);
                            arrayBuffer.Add(currentArrayRegion, elemProp);
                            parseNameToOrdinal[elemPath] = nextDynamicOrdinal;
                            offsetTable.Set(nextDynamicOrdinal, elemProp);
                            nextDynamicOrdinal++;
                            if (node.Validation is not null)
                            {
                                if (elemFieldType == FieldTypes.String)
                                {
                                    string elemStrValue = elemProp.GetString();
                                    BinaryFieldValidator.ValidateString(elemStrValue, "/" + elemPath, node.Validation, node.CompiledPattern, node.ErrorInfo, errors);
                                }
                                else if (elemFieldType != 0 && elemFieldType != FieldTypes.Padding && elemFieldType != FieldTypes.Bits && elemFieldType != FieldTypes.Enum)
                                {
                                    double elemValue = BinaryFieldValidator.ExtractNumericAsDouble(elemProp, elemFieldType);
                                    BinaryFieldValidator.ValidateNumeric(elemValue, "/" + elemPath, node.Validation, node.ErrorInfo, errors);
                                }
                            }
                        }
                    }

                    // Container ParsedProperty wired to ArrayBuffer for enumeration (D-01, D-03)
                    int totalArrayBytes = count * elementSize;
                    var containerProp = new ParsedProperty(
                        data, arrayBaseOffset, totalArrayBytes, "/" + node.Name,
                        /*format:*/ 1, node.ResolvedEndianness, FieldTypes.None,
                        offsetTable, parseNameToOrdinal, arrayBuffer, currentArrayRegion);
                    offsetTable.Set(i, containerProp);

                    continue;
                }

                continue; // unknown composite type
            }

            switch (fieldType)
            {
                case FieldTypes.Padding:
                    // D-13/D-14/D-15: Named entry, Empty value, cursor advance is implicit via AbsoluteOffset
                    // offsetTable slot stays default (ParsedProperty.Empty) -- no Set call needed
                    break;

                case FieldTypes.String:
                {
                    // D-01/D-02/D-04: Encoding + trim mode packed into one byte
                    // Bits 0-1: encoding (0=UTF-8, 1=ASCII), Bits 2-3: mode (0=plain, 1=trimStart, 2=trimEnd, 3=trim)
                    byte encodingByte = (byte)((node.Encoding == "ASCII" ? 1 : 0) | (node.StringMode << 2));
                    var prop = new ParsedProperty(
                        data, node.AbsoluteOffset, node.Size,
                        "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness,
                        FieldTypes.String, encodingByte);
                    offsetTable.Set(i, prop);
                    if (node.Validation is not null)
                    {
                        string strValue = prop.GetString();
                        BinaryFieldValidator.ValidateString(strValue, "/" + node.Name, node.Validation, node.CompiledPattern, node.ErrorInfo, errors);
                    }
                    break;
                }

                case FieldTypes.Enum:
                {
                    // D-05/D-06: Two entries -- base name for raw numeric, suffixed name for string label
                    // Base entry: raw numeric access using the enum's primitive type
                    byte enumPrimitiveType = GetFieldType(node.EnumPrimitive ?? "uint8");
                    var rawProp = new ParsedProperty(
                        data, node.AbsoluteOffset, node.Size,
                        "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness, enumPrimitiveType);
                    offsetTable.Set(i, rawProp);

                    // Suffixed entry: D-07 lazy dictionary lookup, D-08 unmapped fallback
                    if (NameToOrdinal.TryGetValue(node.Name + "s", out int suffixedOrdinal))
                    {
                        var labelProp = new ParsedProperty(
                            data, node.AbsoluteOffset, node.Size,
                            "/" + node.Name + "s", /*format:*/ 1, node.ResolvedEndianness,
                            FieldTypes.Enum, node.EnumValues);
                        offsetTable.Set(suffixedOrdinal, labelProp);
                    }
                    break;
                }

                case FieldTypes.Bits:
                {
                    // D-10: Container itself accessible with raw byte value
                    var containerProp = new ParsedProperty(
                        data, node.AbsoluteOffset, node.Size,
                        "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness,
                        node.Size == 1 ? FieldTypes.UInt8 : FieldTypes.UInt16);
                    offsetTable.Set(i, containerProp);

                    // D-11/D-12: Pre-extract sub-fields at parse time
                    if (node.BitFields is not null)
                    {
                        // Read container value with endianness (D-12)
                        uint containerValue;
                        if (node.Size == 1)
                        {
                            containerValue = data[node.AbsoluteOffset];
                        }
                        else // size == 2
                        {
                            containerValue = node.ResolvedEndianness == 0
                                ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(node.AbsoluteOffset, 2))
                                : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(node.AbsoluteOffset, 2));
                        }

                        // D-09: Extract each sub-field, store at path "containerName/subFieldName"
                        foreach (var (subName, info) in node.BitFields)
                        {
                            uint mask = (1u << info.Bits) - 1;
                            byte extracted = (byte)((containerValue >> info.Bit) & mask);

                            string subPath = "/" + node.Name + "/" + subName;
                            if (NameToOrdinal.TryGetValue(node.Name + "/" + subName, out int subOrdinal))
                            {
                                byte subFieldType = GetFieldType(info.Type);
                                bitScratchBuffer![bitScratchOffset] = extracted;
                                var subProp = new ParsedProperty(
                                    bitScratchBuffer, bitScratchOffset, 1,
                                    subPath, /*format:*/ 1, /*endianness:*/ 0, subFieldType);
                                offsetTable.Set(subOrdinal, subProp);
                                bitScratchOffset++;
                            }
                        }
                    }
                    break;
                }

                default:
                    // Scalar types (existing behavior)
                    var scalarProp = new ParsedProperty(
                        data, node.AbsoluteOffset, node.Size,
                        "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness, fieldType);
                    offsetTable.Set(i, scalarProp);
                    if (node.Validation is not null)
                    {
                        double numValue = BinaryFieldValidator.ExtractNumericAsDouble(scalarProp, fieldType);
                        BinaryFieldValidator.ValidateNumeric(numValue, "/" + node.Name, node.Validation, node.ErrorInfo, errors);
                    }
                    break;
            }
        }

        // Pass 2: Dynamic-offset fields (D-10, D-11)
        if (pass2StartIndex >= 0)
        {
            // Compute running offset from end of last fixed field
            int runningOffset = 0;
            if (pass2StartIndex > 0)
            {
                var lastFixed = OrderedFields[pass2StartIndex - 1];
                int lastFixedSize = ComputeActualFieldSize(lastFixed, offsetTable, parseNameToOrdinal);
                runningOffset = lastFixed.AbsoluteOffset + lastFixedSize;
            }

            for (int i = pass2StartIndex; i < OrderedFields.Length; i++)
            {
                var node = OrderedFields[i];
                int actualOffset = runningOffset;

                byte fieldType = GetFieldType(node.Type);
                if (fieldType == 0)
                {
                    // Handle array type nodes in Pass 2
                    if (node.Type == "array" && node.ArrayElement is not null)
                    {
                        int count;
                        if (node.Count is int arrayCount)
                        {
                            count = arrayCount;
                        }
                        else if (node.Count is string countFieldName)
                        {
                            // Semi-dynamic: resolve count from already-parsed field (D-06)
                            if (parseNameToOrdinal.TryGetValue(countFieldName, out int countOrdinal))
                            {
                                var countProp = offsetTable[countOrdinal];
                                count = ReadCountValue(countProp);
                            }
                            else
                            {
                                count = 0;
                            }
                        }
                        else
                        {
                            continue;
                        }

                        var elemInfo = node.ArrayElement;

                        // Graceful degradation (D-05): clamp count by available payload bytes
                        int elementSize = elemInfo.Size;
                        int availableBytes = data.Length - actualOffset;
                        if (availableBytes < 0) availableBytes = 0;
                        int maxFit = elementSize > 0 ? availableBytes / elementSize : 0;
                        if (maxFit < count) count = maxFit;

                        int currentArrayRegion = arrayRegionIndex++;

                        if (elemInfo.Type == "struct" && elemInfo.StructFields is not null)
                        {
                            for (int e = 0; e < count; e++)
                            {
                                int elementBase = actualOffset + (e * elementSize);

                                foreach (var sf in elemInfo.StructFields)
                                {
                                    int sfOffset = elementBase + sf.AbsoluteOffset;
                                    byte sfFieldType = GetFieldType(sf.Type);
                                    string sfPath = node.Name + "/" + e + "/" + sf.Name;

                                    // Bounds check (D-11)
                                    if (sfOffset + sf.Size > data.Length)
                                        continue;

                                    var sfProp = new ParsedProperty(
                                        data, sfOffset, sf.Size, "/" + sfPath,
                                        /*format:*/ 1, sf.ResolvedEndianness, sfFieldType);
                                    parseNameToOrdinal[sfPath] = nextDynamicOrdinal;
                                    offsetTable.Set(nextDynamicOrdinal, sfProp);
                                    nextDynamicOrdinal++;
                                    if (sf.Validation is not null)
                                    {
                                        byte sfFieldType2 = GetFieldType(sf.Type);
                                        if (sfFieldType2 == FieldTypes.String)
                                        {
                                            string sfStrValue = sfProp.GetString();
                                            BinaryFieldValidator.ValidateString(sfStrValue, "/" + sfPath, sf.Validation, sf.CompiledPattern, sf.ErrorInfo, errors);
                                        }
                                        else if (sfFieldType2 != 0 && sfFieldType2 != FieldTypes.Padding && sfFieldType2 != FieldTypes.Bits && sfFieldType2 != FieldTypes.Enum)
                                        {
                                            double sfNumValue = BinaryFieldValidator.ExtractNumericAsDouble(sfProp, sfFieldType2);
                                            BinaryFieldValidator.ValidateNumeric(sfNumValue, "/" + sfPath, sf.Validation, sf.ErrorInfo, errors);
                                        }
                                    }
                                }

                                var structElemProp = new ParsedProperty(
                                    data, elementBase, elementSize, "/" + node.Name + "/" + e,
                                    /*format:*/ 1, node.ResolvedEndianness, FieldTypes.None,
                                    offsetTable, parseNameToOrdinal, null, -1);
                                arrayBuffer.Add(currentArrayRegion, structElemProp);
                            }
                        }
                        else
                        {
                            byte elemFieldType = GetFieldType(elemInfo.Type);
                            for (int e = 0; e < count; e++)
                            {
                                int elemOffset = actualOffset + (e * elementSize);
                                string elemPath = node.Name + "/" + e;

                                // Bounds check (D-11)
                                if (elemOffset + elementSize > data.Length)
                                    break;

                                var elemProp = new ParsedProperty(
                                    data, elemOffset, elementSize, "/" + elemPath,
                                    /*format:*/ 1, node.ResolvedEndianness, elemFieldType);
                                arrayBuffer.Add(currentArrayRegion, elemProp);
                                parseNameToOrdinal[elemPath] = nextDynamicOrdinal;
                                offsetTable.Set(nextDynamicOrdinal, elemProp);
                                nextDynamicOrdinal++;
                                if (node.Validation is not null)
                                {
                                    if (elemFieldType == FieldTypes.String)
                                    {
                                        string elemStrValue = elemProp.GetString();
                                        BinaryFieldValidator.ValidateString(elemStrValue, "/" + elemPath, node.Validation, node.CompiledPattern, node.ErrorInfo, errors);
                                    }
                                    else if (elemFieldType != 0 && elemFieldType != FieldTypes.Padding && elemFieldType != FieldTypes.Bits && elemFieldType != FieldTypes.Enum)
                                    {
                                        double elemValue = BinaryFieldValidator.ExtractNumericAsDouble(elemProp, elemFieldType);
                                        BinaryFieldValidator.ValidateNumeric(elemValue, "/" + elemPath, node.Validation, node.ErrorInfo, errors);
                                    }
                                }
                            }
                        }

                        int totalArrayBytes = count * elementSize;
                        var containerProp = new ParsedProperty(
                            data, actualOffset, totalArrayBytes, "/" + node.Name,
                            /*format:*/ 1, node.ResolvedEndianness, FieldTypes.None,
                            offsetTable, parseNameToOrdinal, arrayBuffer, currentArrayRegion);
                        offsetTable.Set(i, containerProp);

                        runningOffset += totalArrayBytes;
                        continue;
                    }

                    continue; // unknown composite type
                }

                // Bounds check for non-array fields (D-11)
                if (actualOffset + node.Size > data.Length)
                {
                    runningOffset += node.Size;
                    continue;
                }

                switch (fieldType)
                {
                    case FieldTypes.Padding:
                        runningOffset += node.Size;
                        break;

                    case FieldTypes.String:
                    {
                        byte encodingByte = (byte)((node.Encoding == "ASCII" ? 1 : 0) | (node.StringMode << 2));
                        var prop = new ParsedProperty(
                            data, actualOffset, node.Size,
                            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness,
                            FieldTypes.String, encodingByte);
                        offsetTable.Set(i, prop);
                        if (node.Validation is not null)
                        {
                            string strValue = prop.GetString();
                            BinaryFieldValidator.ValidateString(strValue, "/" + node.Name, node.Validation, node.CompiledPattern, node.ErrorInfo, errors);
                        }
                        runningOffset += node.Size;
                        break;
                    }

                    case FieldTypes.Enum:
                    {
                        byte enumPrimitiveType = GetFieldType(node.EnumPrimitive ?? "uint8");
                        var rawProp = new ParsedProperty(
                            data, actualOffset, node.Size,
                            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness, enumPrimitiveType);
                        offsetTable.Set(i, rawProp);

                        if (NameToOrdinal.TryGetValue(node.Name + "s", out int suffixedOrdinal))
                        {
                            var labelProp = new ParsedProperty(
                                data, actualOffset, node.Size,
                                "/" + node.Name + "s", /*format:*/ 1, node.ResolvedEndianness,
                                FieldTypes.Enum, node.EnumValues);
                            offsetTable.Set(suffixedOrdinal, labelProp);
                        }
                        runningOffset += node.Size;
                        break;
                    }

                    case FieldTypes.Bits:
                    {
                        var containerProp = new ParsedProperty(
                            data, actualOffset, node.Size,
                            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness,
                            node.Size == 1 ? FieldTypes.UInt8 : FieldTypes.UInt16);
                        offsetTable.Set(i, containerProp);

                        if (node.BitFields is not null)
                        {
                            uint containerValue;
                            if (node.Size == 1)
                            {
                                containerValue = data[actualOffset];
                            }
                            else
                            {
                                containerValue = node.ResolvedEndianness == 0
                                    ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(actualOffset, 2))
                                    : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(actualOffset, 2));
                            }

                            foreach (var (subName, info) in node.BitFields)
                            {
                                uint mask = (1u << info.Bits) - 1;
                                byte extracted = (byte)((containerValue >> info.Bit) & mask);

                                string subPath = "/" + node.Name + "/" + subName;
                                if (NameToOrdinal.TryGetValue(node.Name + "/" + subName, out int subOrdinal))
                                {
                                    byte subFieldType = GetFieldType(info.Type);
                                    bitScratchBuffer![bitScratchOffset] = extracted;
                                    var subProp = new ParsedProperty(
                                        bitScratchBuffer, bitScratchOffset, 1,
                                        subPath, /*format:*/ 1, /*endianness:*/ 0, subFieldType);
                                    offsetTable.Set(subOrdinal, subProp);
                                    bitScratchOffset++;
                                }
                            }
                        }
                        runningOffset += node.Size;
                        break;
                    }

                    default:
                        var scalarProp = new ParsedProperty(
                            data, actualOffset, node.Size,
                            "/" + node.Name, /*format:*/ 1, node.ResolvedEndianness, fieldType);
                        offsetTable.Set(i, scalarProp);
                        if (node.Validation is not null)
                        {
                            double numValue = BinaryFieldValidator.ExtractNumericAsDouble(scalarProp, fieldType);
                            BinaryFieldValidator.ValidateNumeric(numValue, "/" + node.Name, node.Validation, node.ErrorInfo, errors);
                        }
                        runningOffset += node.Size;
                        break;
                }
            }
        }

        return new ParseResult(offsetTable, errors, parseNameToOrdinal, arrayBuffer);
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

    /// <summary>
    /// Reads an unsigned integer count from a ParsedProperty using the appropriate typed getter.
    /// Count fields may be uint8, uint16, or uint32 -- calling GetUInt32() on a uint8 field
    /// would throw due to type strictness, so we dispatch based on the field's byte size.
    /// </summary>
    private static int ReadCountValue(ParsedProperty countProp)
    {
        return countProp.RawBytes.Length switch
        {
            1 => countProp.GetUInt8(),
            2 => countProp.GetUInt16(),
            4 => (int)countProp.GetUInt32(),
            _ => 0
        };
    }

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
        "string" => FieldTypes.String,
        "enum" => FieldTypes.Enum,
        "bits" => FieldTypes.Bits,
        "padding" => FieldTypes.Padding,
        _ => 0 // composite: array, struct (Phase 5)
    };

    /// <summary>
    /// Computes the actual byte size of a field at parse time, resolving semi-dynamic array counts
    /// from the OffsetTable. Used by Pass 2 to compute the running offset accumulator start position.
    /// </summary>
    private static int ComputeActualFieldSize(BinaryContractNode node, OffsetTable offsetTable,
        Dictionary<string, int> parseNameToOrdinal)
    {
        if (node.Type != "array" || node.ArrayElement is null)
            return node.Size;

        int count;
        if (node.Count is int fixedCount)
        {
            count = fixedCount;
        }
        else if (node.Count is string countFieldName)
        {
            if (parseNameToOrdinal.TryGetValue(countFieldName, out int countOrdinal))
            {
                var countProp = offsetTable[countOrdinal];
                count = ReadCountValue(countProp);
            }
            else
            {
                count = 0;
            }
        }
        else
        {
            return node.Size;
        }

        return count * node.ArrayElement.Size;
    }

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
