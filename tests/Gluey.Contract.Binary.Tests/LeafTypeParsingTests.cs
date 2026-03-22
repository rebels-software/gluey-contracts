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

    // Contract 3: Bit fields (8-bit container)
    private const string BitField8ContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "flags": {
              "type": "bits",
              "size": 1,
              "fields": {
                "isCharging": { "bit": 0, "bits": 1, "type": "boolean" },
                "errorCode":  { "bit": 1, "bits": 4, "type": "uint8" },
                "priority":   { "bit": 5, "bits": 3, "type": "uint8" }
              }
            }
          }
        }
        """;

    // Contract 4a: 16-bit bit container (big-endian)
    private const string BitField16BigEndianContractJson = """
        {
          "kind": "binary",
          "endianness": "big",
          "fields": {
            "status": {
              "type": "bits",
              "size": 2,
              "fields": {
                "alarm":    { "bit": 0, "bits": 1, "type": "boolean" },
                "severity": { "bit": 1, "bits": 3, "type": "uint8" },
                "code":     { "bit": 8, "bits": 8, "type": "uint8" }
              }
            }
          }
        }
        """;

    // Contract 4b: 16-bit bit container (little-endian)
    private const string BitField16LittleEndianContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "status": {
              "type": "bits",
              "size": 2,
              "fields": {
                "alarm":    { "bit": 0, "bits": 1, "type": "boolean" },
                "severity": { "bit": 1, "bits": 3, "type": "uint8" },
                "code":     { "bit": 8, "bits": 8, "type": "uint8" }
              }
            }
          }
        }
        """;

    // Contract 5: Padding
    // header(uint8,1) + reserved(padding,3) + value(uint16,2) = 6 bytes
    private const string PaddingContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "header":   { "type": "uint8", "size": 1 },
            "reserved": { "dependsOn": "header", "type": "padding", "size": 3 },
            "value":    { "dependsOn": "reserved", "type": "uint16", "size": 2 }
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

    // ================================================================
    // Bit field tests (BITS-01, BITS-02, BITS-03, BITS-04)
    // ================================================================

    [Test]
    public void BitField_8bit_BooleanSubField_ReturnsTrue()
    {
        var schema = BinaryContractSchema.Load(BitField8ContractJson)!;
        // 0x07 = 00000111: bit0=1 (isCharging=true)
        var payload = new byte[] { 0x07 };

        using var result = schema.Parse(payload)!.Value;

        result["flags/isCharging"].GetBoolean().Should().BeTrue();
    }

    [Test]
    public void BitField_8bit_BooleanSubField_ReturnsFalse()
    {
        var schema = BinaryContractSchema.Load(BitField8ContractJson)!;
        // 0x06 = 00000110: bit0=0 (isCharging=false)
        var payload = new byte[] { 0x06 };

        using var result = schema.Parse(payload)!.Value;

        result["flags/isCharging"].GetBoolean().Should().BeFalse();
    }

    [Test]
    public void BitField_8bit_NumericSubField_ExtractsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(BitField8ContractJson)!;
        // 0x07 = 00000111: bits1-4 = 0011 = 3 (errorCode)
        var payload = new byte[] { 0x07 };

        using var result = schema.Parse(payload)!.Value;

        result["flags/errorCode"].GetUInt8().Should().Be(3);
    }

    [Test]
    public void BitField_8bit_MultipleSubFields()
    {
        var schema = BinaryContractSchema.Load(BitField8ContractJson)!;
        // 0xA7 = 10100111:
        //   bit0 = 1 -> isCharging = true
        //   bits1-4 = 0011 -> errorCode = 3
        //   bits5-7 = 101 -> priority = 5
        var payload = new byte[] { 0xA7 };

        using var result = schema.Parse(payload)!.Value;

        result["flags/isCharging"].GetBoolean().Should().BeTrue();
        result["flags/errorCode"].GetUInt8().Should().Be(3);
        result["flags/priority"].GetUInt8().Should().Be(5);
    }

    [Test]
    public void BitField_8bit_ContainerAccessible()
    {
        var schema = BinaryContractSchema.Load(BitField8ContractJson)!;
        // D-10: container itself is accessible
        var payload = new byte[] { 0xA7 };

        using var result = schema.Parse(payload)!.Value;

        result["flags"].GetUInt8().Should().Be(0xA7);
    }

    [Test]
    public void BitField_16bit_BigEndian_ExtractsCorrectly()
    {
        var schema = BinaryContractSchema.Load(BitField16BigEndianContractJson)!;
        // Big-endian bytes [0x03, 0x05] -> uint16 = 0x0305
        // 0x0305 = 0000001100000101
        //   bit0 = 1 -> alarm = true
        //   bits1-3 = 010 -> severity = 2
        //   bits8-15 = 00000011 -> code = 3
        var payload = new byte[] { 0x03, 0x05 };

        using var result = schema.Parse(payload)!.Value;

        result["status/alarm"].GetBoolean().Should().BeTrue();
        result["status/severity"].GetUInt8().Should().Be(2);
        result["status/code"].GetUInt8().Should().Be(3);
    }

    [Test]
    public void BitField_16bit_LittleEndian_ExtractsCorrectly()
    {
        var schema = BinaryContractSchema.Load(BitField16LittleEndianContractJson)!;
        // Same logical value 0x0305 but little-endian: bytes [0x05, 0x03]
        // uint16 LE read of [0x05, 0x03] = 0x0305
        //   bit0 = 1 -> alarm = true
        //   bits1-3 = 010 -> severity = 2
        //   bits8-15 = 00000011 -> code = 3
        var payload = new byte[] { 0x05, 0x03 };

        using var result = schema.Parse(payload)!.Value;

        result["status/alarm"].GetBoolean().Should().BeTrue();
        result["status/severity"].GetUInt8().Should().Be(2);
        result["status/code"].GetUInt8().Should().Be(3);
    }

    [Test]
    public void BitField_16bit_ContainerAccessible()
    {
        var schema = BinaryContractSchema.Load(BitField16BigEndianContractJson)!;
        // D-10: 16-bit container accessible
        var payload = new byte[] { 0x03, 0x05 };

        using var result = schema.Parse(payload)!.Value;

        result["status"].GetUInt16().Should().Be(0x0305);
    }

    // ================================================================
    // Padding tests (COMP-04)
    // ================================================================

    [Test]
    public void Padding_FieldNotExposedInResult()
    {
        var schema = BinaryContractSchema.Load(PaddingContractJson)!;
        // header=0xFF, reserved=3 bytes padding, value=0x012C (300 LE)
        var payload = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["reserved"].HasValue.Should().BeFalse();
    }

    [Test]
    public void Padding_SkipsBytesCorrectly()
    {
        var schema = BinaryContractSchema.Load(PaddingContractJson)!;
        // header=0xFF, reserved=3 bytes padding, value=0x012C (300 LE) at offset 4
        var payload = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["value"].GetUInt16().Should().Be(300);
    }

    [Test]
    public void Padding_HeaderStillAccessible()
    {
        var schema = BinaryContractSchema.Load(PaddingContractJson)!;
        var payload = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["header"].GetUInt8().Should().Be(0xFF);
    }
}
