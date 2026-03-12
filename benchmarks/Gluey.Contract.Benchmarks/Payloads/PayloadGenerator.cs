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
using System.Text.Json;

namespace Gluey.Contract.Benchmarks.Payloads;

/// <summary>
/// Generates valid JSON payloads at various sizes for benchmark scenarios.
/// Each method produces a byte[] suitable for passing to JsonContractSchema.TryParse.
/// </summary>
public static class PayloadGenerator
{
    /// <summary>
    /// Generates a flat JSON object with string, integer, boolean, number, and email properties.
    /// Repeats property groups to approximate the target byte size.
    /// </summary>
    public static byte[] GenerateFlat(int targetBytes)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("name", "John Doe");
        writer.WriteNumber("age", 42);
        writer.WriteBoolean("active", true);
        writer.WriteNumber("score", 98.6);
        writer.WriteString("email", "john@example.com");

        int i = 0;
        while (writer.BytesCommitted + writer.BytesPending < targetBytes - 50)
        {
            writer.WriteString($"extra_{i}", $"value_{i}_padding_data");
            writer.WriteNumber($"num_{i}", i * 17);
            i++;
        }

        writer.WriteEndObject();
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Generates a nested JSON object with address and contact sub-objects, 2-3 levels deep.
    /// </summary>
    public static byte[] GenerateNested(int targetBytes)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("name", "Jane Smith");

        writer.WriteStartObject("address");
        writer.WriteString("street", "123 Main St");
        writer.WriteString("city", "Springfield");
        writer.WriteString("zip", "62704");
        writer.WriteStartObject("geo");
        writer.WriteNumber("lat", 39.7817);
        writer.WriteNumber("lng", -89.6501);
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartObject("contact");
        writer.WriteString("phone", "+1-555-0100");
        writer.WriteString("email", "jane@example.com");
        writer.WriteEndObject();

        int i = 0;
        while (writer.BytesCommitted + writer.BytesPending < targetBytes - 100)
        {
            writer.WriteStartObject($"section_{i}");
            writer.WriteString("label", $"section_label_{i}");
            writer.WriteNumber("value", i * 3.14);
            writer.WriteStartObject("detail");
            writer.WriteString("info", $"detail_info_padding_{i}");
            writer.WriteEndObject();
            writer.WriteEndObject();
            i++;
        }

        writer.WriteEndObject();
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Generates a JSON object with an array of item objects (3-4 properties each).
    /// </summary>
    public static byte[] GenerateArray(int targetBytes)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteStartArray("items");

        int i = 0;
        while (writer.BytesCommitted + writer.BytesPending < targetBytes - 50)
        {
            writer.WriteStartObject();
            writer.WriteString("id", $"item-{i:D4}");
            writer.WriteString("label", $"Item number {i}");
            writer.WriteNumber("quantity", i % 100);
            writer.WriteBoolean("available", i % 2 == 0);
            writer.WriteEndObject();
            i++;
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>
    /// Generates a complex JSON payload exercising composition, conditionals, patterns,
    /// and various validation constraints.
    /// </summary>
    public static byte[] GenerateFullSchema(int targetBytes)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "premium");
        writer.WriteString("name", "Full Schema Test");
        writer.WriteNumber("age", 30);
        writer.WriteString("email", "test@example.com");
        writer.WriteString("phone", "+1-555-0199");
        writer.WriteNumber("rating", 4.5);
        writer.WriteString("code", "ABC-123");

        writer.WriteStartObject("billing");
        writer.WriteString("method", "credit_card");
        writer.WriteString("cardNumber", "4111111111111111");
        writer.WriteEndObject();

        writer.WriteStartObject("shipping");
        writer.WriteString("street", "456 Oak Ave");
        writer.WriteString("city", "Portland");
        writer.WriteString("zip", "97201");
        writer.WriteEndObject();

        int i = 0;
        while (writer.BytesCommitted + writer.BytesPending < targetBytes - 100)
        {
            writer.WriteString($"field_{i}", $"value_padding_{i}_extra_data_here");
            i++;
        }

        writer.WriteEndObject();
        writer.Flush();

        return stream.ToArray();
    }
}
