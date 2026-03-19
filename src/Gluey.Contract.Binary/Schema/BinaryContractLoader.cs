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

using System.Text.Json;
using Gluey.Contract.Binary.Dto;

namespace Gluey.Contract.Binary.Schema;

/// <summary>
/// Deserializes binary contract JSON into DTOs and maps them to a <see cref="BinaryContractNode"/> dictionary.
/// </summary>
internal static class BinaryContractLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads a binary contract from UTF-8 JSON bytes.
    /// </summary>
    /// <returns>
    /// A tuple of (fields dictionary keyed by name, contract-level endianness, metadata).
    /// Returns (null, null, null) on invalid JSON or wrong kind.
    /// </returns>
    internal static (Dictionary<string, BinaryContractNode>? Fields, string? ContractEndianness, ContractMetadata? Metadata) Load(
        ReadOnlySpan<byte> utf8Json, ErrorCollector errors)
    {
        // -- Deserialize JSON into DTO --

        ContractDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ContractDto>(utf8Json, s_options);
        }
        catch (JsonException)
        {
            errors.Add(new ValidationError(
                string.Empty,
                ValidationErrorCode.InvalidJson,
                ValidationErrorMessages.Get(ValidationErrorCode.InvalidJson)));
            return (null, null, null);
        }

        if (dto is null)
        {
            errors.Add(new ValidationError(
                string.Empty,
                ValidationErrorCode.InvalidJson,
                ValidationErrorMessages.Get(ValidationErrorCode.InvalidJson)));
            return (null, null, null);
        }

        // -- Validate kind --

        if (dto.Kind != "binary")
        {
            errors.Add(new ValidationError(
                "/kind",
                ValidationErrorCode.InvalidKind,
                ValidationErrorMessages.Get(ValidationErrorCode.InvalidKind)));
            return (null, null, null);
        }

        // -- Map fields --

        if (dto.Fields is null || dto.Fields.Count == 0)
        {
            return (new Dictionary<string, BinaryContractNode>(), dto.Endianness,
                new ContractMetadata(dto.Id, dto.Name, dto.Version, dto.DisplayName));
        }

        var fields = new Dictionary<string, BinaryContractNode>(dto.Fields.Count);
        foreach (var (name, fieldDto) in dto.Fields)
        {
            fields[name] = MapField(name, fieldDto);
        }

        return (fields, dto.Endianness,
            new ContractMetadata(dto.Id, dto.Name, dto.Version, dto.DisplayName));
    }

    // -- Field mapping --

    private static BinaryContractNode MapField(string name, FieldDto dto)
    {
        var fieldType = dto.Type ?? "";

        return new BinaryContractNode
        {
            Name = name,
            DependsOn = dto.DependsOn,
            Type = fieldType,
            Size = dto.Size ?? 0,
            Encoding = dto.Encoding,
            Endianness = dto.Endianness,
            DisplayName = dto.DisplayName,
            XDescription = dto.XDescription,

            // Type-specific mappings
            BitFields = fieldType == "bits" ? DeserializeBitFields(dto.Fields) : null,
            ArrayElement = MapArrayElement(dto.Element),
            EnumValues = dto.Values,
            EnumPrimitive = dto.Primitive,
            Count = MapCount(dto.Count),
            StructFields = MapStructFields(dto.Element),

            // Validation
            Validation = MapValidation(dto.Validation),

            // Extensions
            ErrorInfo = MapErrorInfo(dto.XError),
        };
    }

    private static Dictionary<string, BitFieldInfo>? DeserializeBitFields(JsonElement? fieldsElement)
    {
        if (fieldsElement is null || fieldsElement.Value.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, BitFieldInfo>();
        foreach (var prop in fieldsElement.Value.EnumerateObject())
        {
            int bit = prop.Value.TryGetProperty("bit", out var bitProp) ? bitProp.GetInt32() : 0;
            int bits = prop.Value.TryGetProperty("bits", out var bitsProp) ? bitsProp.GetInt32() : 0;
            string type = prop.Value.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
            result[prop.Name] = new BitFieldInfo(bit, bits, type);
        }

        return result.Count > 0 ? result : null;
    }

    private static Dictionary<string, FieldDto>? DeserializeStructFields(JsonElement? fieldsElement)
    {
        if (fieldsElement is null || fieldsElement.Value.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, FieldDto>();
        foreach (var prop in fieldsElement.Value.EnumerateObject())
        {
            var fieldDto = JsonSerializer.Deserialize<FieldDto>(prop.Value.GetRawText(), s_options);
            if (fieldDto is not null)
                result[prop.Name] = fieldDto;
        }

        return result.Count > 0 ? result : null;
    }

    private static ArrayElementInfo? MapArrayElement(FieldDto? elementDto)
    {
        if (elementDto is null)
            return null;

        BinaryContractNode[]? structFields = null;
        if (elementDto.Type == "struct")
        {
            var subFields = DeserializeStructFields(elementDto.Fields);
            if (subFields is not null)
            {
                structFields = new BinaryContractNode[subFields.Count];
                int i = 0;
                foreach (var (subName, subDto) in subFields)
                {
                    structFields[i++] = MapField(subName, subDto);
                }
            }
        }

        return new ArrayElementInfo(
            elementDto.Type ?? "",
            elementDto.Size ?? 0,
            structFields);
    }

    private static BinaryContractNode[]? MapStructFields(FieldDto? elementDto)
    {
        if (elementDto is null || elementDto.Type != "struct")
            return null;

        var subFields = DeserializeStructFields(elementDto.Fields);
        if (subFields is null)
            return null;

        var nodes = new BinaryContractNode[subFields.Count];
        int i = 0;
        foreach (var (subName, subDto) in subFields)
        {
            nodes[i++] = MapField(subName, subDto);
        }

        return nodes;
    }

    private static object? MapCount(JsonElement? countElement)
    {
        if (countElement is null || countElement.Value.ValueKind == JsonValueKind.Undefined)
            return null;

        var element = countElement.Value;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int intCount))
            return intCount;

        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();

        return null;
    }

    private static ValidationRules? MapValidation(ValidationDto? dto)
    {
        if (dto is null)
            return null;

        return new ValidationRules(dto.Min, dto.Max, dto.Pattern, dto.MinLength, dto.MaxLength);
    }

    private static SchemaErrorInfo? MapErrorInfo(JsonElement? xError)
    {
        if (xError is null || xError.Value.ValueKind == JsonValueKind.Undefined)
            return null;

        var element = xError.Value;
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        string? code = element.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
        string? title = element.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
        string? detail = element.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : null;
        string? type = element.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

        return new SchemaErrorInfo(code, title, detail, type);
    }
}
