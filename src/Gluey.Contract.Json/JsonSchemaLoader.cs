using System.Buffers;
using System.Text.Json;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Recursive-descent parser that reads a JSON Schema document using <see cref="Utf8JsonReader"/>
/// and produces an immutable <see cref="SchemaNode"/> tree.
/// Handles all Draft 2020-12 keywords. Unknown keywords are silently skipped.
/// </summary>
internal static class JsonSchemaLoader
{
    /// <summary>
    /// Loads a JSON Schema from raw UTF-8 bytes.
    /// Returns <c>null</c> if the input is not valid JSON or not a schema (object or boolean).
    /// Never throws.
    /// </summary>
    internal static SchemaNode? Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            if (!reader.Read())
                return null;

            return ReadSchema(ref reader, "");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SchemaNode? ReadSchema(ref Utf8JsonReader reader, string currentPath)
    {
        // Boolean schemas
        if (reader.TokenType == JsonTokenType.True)
            return SchemaNode.True;
        if (reader.TokenType == JsonTokenType.False)
            return SchemaNode.False;

        // Must be an object
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        // Keyword storage
        string? id = null;
        string? anchor = null;
        string? @ref = null;
        string? dynamicRef = null;
        string? dynamicAnchor = null;
        Dictionary<string, SchemaNode>? defs = null;
        string? comment = null;
        SchemaType? type = null;
        byte[][]? @enum = null;
        byte[]? @const = null;
        decimal? minimum = null;
        decimal? maximum = null;
        decimal? exclusiveMinimum = null;
        decimal? exclusiveMaximum = null;
        decimal? multipleOf = null;
        int? minLength = null;
        int? maxLength = null;
        string? pattern = null;
        int? minItems = null;
        int? maxItems = null;
        int? minContains = null;
        int? maxContains = null;
        bool? uniqueItems = null;
        string[]? required = null;
        int? minProperties = null;
        int? maxProperties = null;
        Dictionary<string, string[]>? dependentRequired = null;
        Dictionary<string, SchemaNode>? properties = null;
        SchemaNode? additionalProperties = null;
        Dictionary<string, SchemaNode>? patternProperties = null;
        SchemaNode? propertyNames = null;
        Dictionary<string, SchemaNode>? dependentSchemas = null;
        SchemaNode? items = null;
        SchemaNode[]? prefixItems = null;
        SchemaNode? contains = null;
        SchemaNode[]? allOf = null;
        SchemaNode[]? anyOf = null;
        SchemaNode[]? oneOf = null;
        SchemaNode? not = null;
        SchemaNode? @if = null;
        SchemaNode? then = null;
        SchemaNode? @else = null;
        string? title = null;
        string? description = null;
        string? format = null;
        bool? deprecated = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            // Most common keywords first for early exit
            if (reader.ValueTextEquals("type"u8))
            {
                reader.Read();
                type = ReadType(ref reader);
            }
            else if (reader.ValueTextEquals("properties"u8))
            {
                reader.Read();
                properties = ReadSchemaMap(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("required"u8))
            {
                reader.Read();
                required = ReadStringArray(ref reader);
            }
            else if (reader.ValueTextEquals("items"u8))
            {
                reader.Read();
                items = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("additionalProperties"u8))
            {
                reader.Read();
                additionalProperties = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("allOf"u8))
            {
                reader.Read();
                allOf = ReadSchemaArray(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("anyOf"u8))
            {
                reader.Read();
                anyOf = ReadSchemaArray(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("oneOf"u8))
            {
                reader.Read();
                oneOf = ReadSchemaArray(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("not"u8))
            {
                reader.Read();
                not = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("if"u8))
            {
                reader.Read();
                @if = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("then"u8))
            {
                reader.Read();
                then = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("else"u8))
            {
                reader.Read();
                @else = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("$ref"u8))
            {
                reader.Read();
                @ref = reader.GetString();
            }
            else if (reader.ValueTextEquals("$defs"u8))
            {
                reader.Read();
                defs = ReadSchemaMap(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("$id"u8))
            {
                reader.Read();
                id = reader.GetString();
            }
            else if (reader.ValueTextEquals("$anchor"u8))
            {
                reader.Read();
                anchor = reader.GetString();
            }
            else if (reader.ValueTextEquals("$dynamicRef"u8))
            {
                reader.Read();
                dynamicRef = reader.GetString();
            }
            else if (reader.ValueTextEquals("$dynamicAnchor"u8))
            {
                reader.Read();
                dynamicAnchor = reader.GetString();
            }
            else if (reader.ValueTextEquals("$comment"u8))
            {
                reader.Read();
                comment = reader.GetString();
            }
            else if (reader.ValueTextEquals("enum"u8))
            {
                reader.Read();
                @enum = ReadEnum(ref reader);
            }
            else if (reader.ValueTextEquals("const"u8))
            {
                reader.Read();
                @const = ReadConst(ref reader);
            }
            else if (reader.ValueTextEquals("minimum"u8))
            {
                reader.Read();
                minimum = reader.GetDecimal();
            }
            else if (reader.ValueTextEquals("maximum"u8))
            {
                reader.Read();
                maximum = reader.GetDecimal();
            }
            else if (reader.ValueTextEquals("exclusiveMinimum"u8))
            {
                reader.Read();
                exclusiveMinimum = reader.GetDecimal();
            }
            else if (reader.ValueTextEquals("exclusiveMaximum"u8))
            {
                reader.Read();
                exclusiveMaximum = reader.GetDecimal();
            }
            else if (reader.ValueTextEquals("multipleOf"u8))
            {
                reader.Read();
                multipleOf = reader.GetDecimal();
            }
            else if (reader.ValueTextEquals("minLength"u8))
            {
                reader.Read();
                minLength = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("maxLength"u8))
            {
                reader.Read();
                maxLength = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("pattern"u8))
            {
                reader.Read();
                pattern = reader.GetString();
            }
            else if (reader.ValueTextEquals("minItems"u8))
            {
                reader.Read();
                minItems = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("maxItems"u8))
            {
                reader.Read();
                maxItems = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("minContains"u8))
            {
                reader.Read();
                minContains = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("maxContains"u8))
            {
                reader.Read();
                maxContains = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("uniqueItems"u8))
            {
                reader.Read();
                uniqueItems = reader.GetBoolean();
            }
            else if (reader.ValueTextEquals("minProperties"u8))
            {
                reader.Read();
                minProperties = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("maxProperties"u8))
            {
                reader.Read();
                maxProperties = reader.GetInt32();
            }
            else if (reader.ValueTextEquals("dependentRequired"u8))
            {
                reader.Read();
                dependentRequired = ReadDependentRequired(ref reader);
            }
            else if (reader.ValueTextEquals("patternProperties"u8))
            {
                reader.Read();
                patternProperties = ReadSchemaMap(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("propertyNames"u8))
            {
                reader.Read();
                propertyNames = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("dependentSchemas"u8))
            {
                reader.Read();
                dependentSchemas = ReadSchemaMap(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("prefixItems"u8))
            {
                reader.Read();
                prefixItems = ReadSchemaArray(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("contains"u8))
            {
                reader.Read();
                contains = ReadSchemaOrBoolean(ref reader, currentPath);
            }
            else if (reader.ValueTextEquals("title"u8))
            {
                reader.Read();
                title = reader.GetString();
            }
            else if (reader.ValueTextEquals("description"u8))
            {
                reader.Read();
                description = reader.GetString();
            }
            else if (reader.ValueTextEquals("format"u8))
            {
                reader.Read();
                format = reader.GetString();
            }
            else if (reader.ValueTextEquals("deprecated"u8))
            {
                reader.Read();
                deprecated = reader.GetBoolean();
            }
            else
            {
                // Unknown keyword -- skip the value
                reader.Read();
                reader.Skip();
            }
        }

        return new SchemaNode(
            path: currentPath,
            id: id,
            anchor: anchor,
            @ref: @ref,
            dynamicRef: dynamicRef,
            dynamicAnchor: dynamicAnchor,
            defs: defs,
            comment: comment,
            type: type,
            @enum: @enum,
            @const: @const,
            minimum: minimum,
            maximum: maximum,
            exclusiveMinimum: exclusiveMinimum,
            exclusiveMaximum: exclusiveMaximum,
            multipleOf: multipleOf,
            minLength: minLength,
            maxLength: maxLength,
            pattern: pattern,
            minItems: minItems,
            maxItems: maxItems,
            minContains: minContains,
            maxContains: maxContains,
            uniqueItems: uniqueItems,
            required: required,
            minProperties: minProperties,
            maxProperties: maxProperties,
            dependentRequired: dependentRequired,
            properties: properties,
            additionalProperties: additionalProperties,
            patternProperties: patternProperties,
            propertyNames: propertyNames,
            dependentSchemas: dependentSchemas,
            items: items,
            prefixItems: prefixItems,
            contains: contains,
            allOf: allOf,
            anyOf: anyOf,
            oneOf: oneOf,
            not: not,
            @if: @if,
            then: then,
            @else: @else,
            title: title,
            description: description,
            format: format,
            deprecated: deprecated);
    }

    // ── Typed reader helpers ──────────────────────────────────────────

    /// <summary>Reads the "type" keyword -- either a single string or array of strings.</summary>
    private static SchemaType? ReadType(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return ParseSchemaType(reader.GetString()!);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            SchemaType combined = SchemaType.None;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                combined |= ParseSchemaType(reader.GetString()!);
            }
            return combined;
        }

        return null;
    }

    private static SchemaType ParseSchemaType(string typeName) => typeName switch
    {
        "null" => SchemaType.Null,
        "boolean" => SchemaType.Boolean,
        "integer" => SchemaType.Integer,
        "number" => SchemaType.Number,
        "string" => SchemaType.String,
        "array" => SchemaType.Array,
        "object" => SchemaType.Object,
        _ => SchemaType.None,
    };

    /// <summary>Reads a sub-schema (object) or boolean schema (true/false).</summary>
    private static SchemaNode? ReadSchemaOrBoolean(ref Utf8JsonReader reader, string parentPath)
    {
        return ReadSchema(ref reader, parentPath);
    }

    /// <summary>Reads an array of schemas [schema, ...].</summary>
    private static SchemaNode[]? ReadSchemaArray(ref Utf8JsonReader reader, string parentPath)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var list = new List<SchemaNode>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var child = ReadSchema(ref reader, parentPath);
            if (child is not null)
                list.Add(child);
        }
        return list.ToArray();
    }

    /// <summary>Reads a map of named schemas {"key": schema, ...}. Builds child paths for properties.</summary>
    private static Dictionary<string, SchemaNode>? ReadSchemaMap(ref Utf8JsonReader reader, string parentPath)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        var map = new Dictionary<string, SchemaNode>();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            string childPath = SchemaNode.BuildChildPath(parentPath, key);
            reader.Read();
            var child = ReadSchema(ref reader, childPath);
            if (child is not null)
                map[key] = child;
        }
        return map;
    }

    /// <summary>Reads an array of strings ["a", "b"].</summary>
    private static string[]? ReadStringArray(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(reader.GetString()!);
        }
        return list.ToArray();
    }

    /// <summary>Reads dependentRequired: {"key": ["a", "b"], ...}.</summary>
    private static Dictionary<string, string[]>? ReadDependentRequired(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        var map = new Dictionary<string, string[]>();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            reader.Read();
            var values = ReadStringArray(ref reader);
            if (values is not null)
                map[key] = values;
        }
        return map;
    }

    /// <summary>Reads enum array, serializing each value to raw UTF-8 bytes.</summary>
    private static byte[][]? ReadEnum(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var list = new List<byte[]>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(SerializeCurrentValue(ref reader));
        }
        return list.ToArray();
    }

    /// <summary>Reads a const value, serializing to raw UTF-8 bytes.</summary>
    private static byte[] ReadConst(ref Utf8JsonReader reader)
    {
        return SerializeCurrentValue(ref reader);
    }

    /// <summary>
    /// Serializes the current JSON value (at the reader's position) to a byte array.
    /// Uses ArrayBufferWriter + Utf8JsonWriter for zero-copy serialization.
    /// </summary>
    private static byte[] SerializeCurrentValue(ref Utf8JsonReader reader)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                writer.WriteStringValue(reader.GetString());
                break;
            case JsonTokenType.Number:
                writer.WriteRawValue(reader.ValueSpan);
                break;
            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonTokenType.Null:
                writer.WriteNullValue();
                break;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                WriteComplexValue(ref reader, writer);
                break;
        }

        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Writes a complex JSON value (object or array) recursively.</summary>
    private static void WriteComplexValue(ref Utf8JsonReader reader, Utf8JsonWriter writer)
    {
        int depth = 0;
        do
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    depth++;
                    break;
                case JsonTokenType.EndObject:
                    writer.WriteEndObject();
                    depth--;
                    break;
                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    depth++;
                    break;
                case JsonTokenType.EndArray:
                    writer.WriteEndArray();
                    depth--;
                    break;
                case JsonTokenType.PropertyName:
                    writer.WritePropertyName(reader.GetString()!);
                    break;
                case JsonTokenType.String:
                    writer.WriteStringValue(reader.GetString());
                    break;
                case JsonTokenType.Number:
                    writer.WriteRawValue(reader.ValueSpan);
                    break;
                case JsonTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;
            }
        } while (depth > 0 && reader.Read());
    }
}
