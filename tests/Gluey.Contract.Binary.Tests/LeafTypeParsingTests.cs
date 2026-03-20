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
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
internal sealed class LeafTypeParsingTests
{
    // ================================================================
    // Contract JSON definitions
    // ================================================================

    // Contract 1: String fields (ASCII + UTF-8 with trim modes)
    // badgeId(string/ASCII,6) + sensorName(string/UTF-8,10) + plainField(string/ASCII,4) + trimBothField(string/UTF-8,6) = 26 bytes
    private const string StringContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "badgeId":       { "type": "string", "encoding": "ASCII", "size": 6 },
            "sensorName":    { "dependsOn": "badgeId", "type": "string", "encoding": "UTF-8", "size": 10 },
            "plainField":    { "dependsOn": "sensorName", "type": "string", "encoding": "ASCII", "size": 4, "mode": "plain" },
            "trimBothField": { "dependsOn": "plainField", "type": "string", "encoding": "UTF-8", "size": 6, "mode": "trim" }
          }
        }
        """;

    // Contract 1b: String with trimStart mode
    private const string TrimStartContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "label": { "type": "string", "encoding": "ASCII", "size": 6, "mode": "trimStart" }
          }
        }
        """;

    // Contract 2: Enum fields
    private const string EnumContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "mode": {
              "type": "enum",
              "primitive": "uint8",
              "size": 1,
              "values": { "0": "idle", "1": "charging", "2": "discharging" }
            }
          }
        }
        """;

    // ================================================================
    // String tests (STRE-01, STRE-02)
    // ================================================================

    [Test]
    public void AsciiString_ReadsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        // "ABC123" in ASCII at offset 0
        Encoding.ASCII.GetBytes("ABC123", payload.AsSpan(0, 6));

        using var result = schema.Parse(payload)!.Value;

        result["badgeId"].GetString().Should().Be("ABC123");
    }

    [Test]
    public void AsciiString_TrimEnd_RemovesTrailingNulls()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        // "AB" followed by 4 null bytes at offset 0
        Encoding.ASCII.GetBytes("AB", payload.AsSpan(0, 2));
        // rest is already 0x00

        using var result = schema.Parse(payload)!.Value;

        result["badgeId"].GetString().Should().Be("AB");
    }

    [Test]
    public void Utf8String_ReadsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        // Fill badgeId with valid ASCII first (6 bytes)
        Encoding.ASCII.GetBytes("BADGE1", payload.AsSpan(0, 6));
        // "Temp\u00B0C" = "Temp" + degree symbol + "C" in UTF-8 (7 bytes: 4+2+1)
        var utf8Bytes = Encoding.UTF8.GetBytes("Temp\u00B0C");
        utf8Bytes.CopyTo(payload.AsSpan(6));

        using var result = schema.Parse(payload)!.Value;

        result["sensorName"].GetString().Should().Be("Temp\u00B0C");
    }

    [Test]
    public void Utf8String_TrimEnd_RemovesTrailingNulls()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        Encoding.ASCII.GetBytes("BADGE1", payload.AsSpan(0, 6));
        // "Hi" at offset 6, rest is 0x00
        Encoding.UTF8.GetBytes("Hi", payload.AsSpan(6, 2));

        using var result = schema.Parse(payload)!.Value;

        result["sensorName"].GetString().Should().Be("Hi");
    }

    [Test]
    public void PlainMode_KeepsNullBytes()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        // Fill preceding fields
        Encoding.ASCII.GetBytes("BADGE1", payload.AsSpan(0, 6));
        Encoding.UTF8.GetBytes("SensorName", payload.AsSpan(6, 10));
        // "AB\0\0" at offset 16 (plain mode)
        payload[16] = (byte)'A';
        payload[17] = (byte)'B';
        payload[18] = 0x00;
        payload[19] = 0x00;

        using var result = schema.Parse(payload)!.Value;

        result["plainField"].GetString().Should().Be("AB\0\0");
    }

    [Test]
    public void TrimMode_RemovesBothEnds()
    {
        var schema = BinaryContractSchema.Load(StringContractJson)!;
        var payload = new byte[26];
        // Fill preceding fields
        Encoding.ASCII.GetBytes("BADGE1", payload.AsSpan(0, 6));
        Encoding.UTF8.GetBytes("SensorName", payload.AsSpan(6, 10));
        Encoding.ASCII.GetBytes("ABCD", payload.AsSpan(16, 4));
        // "\0\0Hi\0\0" at offset 20 (trim mode)
        payload[20] = 0x00;
        payload[21] = 0x00;
        payload[22] = (byte)'H';
        payload[23] = (byte)'i';
        payload[24] = 0x00;
        payload[25] = 0x00;

        using var result = schema.Parse(payload)!.Value;

        result["trimBothField"].GetString().Should().Be("Hi");
    }

    [Test]
    public void TrimStartMode_RemovesLeadingNulls()
    {
        var schema = BinaryContractSchema.Load(TrimStartContractJson)!;
        // "\0\0\0Hi\0" -- trimStart should remove leading nulls but keep trailing
        var payload = new byte[] { 0x00, 0x00, 0x00, (byte)'H', (byte)'i', 0x00 };

        using var result = schema.Parse(payload)!.Value;

        result["label"].GetString().Should().Be("Hi\0");
    }

    // ================================================================
    // Enum tests (STRE-03, STRE-04)
    // ================================================================

    [Test]
    public void Enum_RawNumeric_ReturnsUInt8Value()
    {
        var schema = BinaryContractSchema.Load(EnumContractJson)!;
        var payload = new byte[] { 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["mode"].GetUInt8().Should().Be(1);
    }

    [Test]
    public void Enum_MappedLabel_ReturnsString()
    {
        var schema = BinaryContractSchema.Load(EnumContractJson)!;
        var payload = new byte[] { 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["modes"].GetString().Should().Be("charging");
    }

    [Test]
    public void Enum_UnmappedValue_ReturnsNumericString()
    {
        var schema = BinaryContractSchema.Load(EnumContractJson)!;
        var payload = new byte[] { 0x05 };

        using var result = schema.Parse(payload)!.Value;

        result["modes"].GetString().Should().Be("5");
    }

    [Test]
    public void Enum_ZeroValue_MapsToIdle()
    {
        var schema = BinaryContractSchema.Load(EnumContractJson)!;
        var payload = new byte[] { 0x00 };

        using var result = schema.Parse(payload)!.Value;

        result["modes"].GetString().Should().Be("idle");
    }
}
