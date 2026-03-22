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
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
internal sealed class ScalarParsingTests
{
    // ================================================================
    // Contract JSON definitions
    // ================================================================

    // Test contract 1: Little-endian scalars (22 bytes total)
    // temperature(int16,2) + humidity(uint8,1) + voltage(uint16,2) + current(int32,4)
    // + power(float32,4) + energy(float64,8) + active(boolean,1) = 22
    private const string LittleEndianContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": { "type": "int16", "size": 2 },
            "humidity":    { "dependsOn": "temperature", "type": "uint8",   "size": 1 },
            "voltage":     { "dependsOn": "humidity",    "type": "uint16",  "size": 2 },
            "current":     { "dependsOn": "voltage",     "type": "int32",   "size": 4 },
            "power":       { "dependsOn": "current",     "type": "float32", "size": 4 },
            "energy":      { "dependsOn": "power",       "type": "float64", "size": 8 },
            "active":      { "dependsOn": "energy",      "type": "boolean", "size": 1 }
          }
        }
        """;

    // Test contract 2: Big-endian scalars (same fields, 22 bytes)
    private const string BigEndianContractJson = """
        {
          "kind": "binary",
          "endianness": "big",
          "fields": {
            "temperature": { "type": "int16", "size": 2 },
            "humidity":    { "dependsOn": "temperature", "type": "uint8",   "size": 1 },
            "voltage":     { "dependsOn": "humidity",    "type": "uint16",  "size": 2 },
            "current":     { "dependsOn": "voltage",     "type": "int32",   "size": 4 },
            "power":       { "dependsOn": "current",     "type": "float32", "size": 4 },
            "energy":      { "dependsOn": "power",       "type": "float64", "size": 8 },
            "active":      { "dependsOn": "energy",      "type": "boolean", "size": 1 }
          }
        }
        """;

    // Test contract 3: Truncated numerics (big-endian, 6 bytes)
    // truncatedSigned(int32,3) + truncatedUnsigned(uint32,3)
    private const string TruncatedContractJson = """
        {
          "kind": "binary",
          "endianness": "big",
          "fields": {
            "truncatedSigned":   { "type": "int32",  "size": 3 },
            "truncatedUnsigned": { "dependsOn": "truncatedSigned", "type": "uint32", "size": 3 }
          }
        }
        """;

    // Test contract 4: Type strictness (2 bytes)
    private const string TypeStrictnessContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "myUint16": { "type": "uint16", "size": 2 }
          }
        }
        """;

    // Test contract 5: Non-scalar field skip (6 bytes)
    // byte1(uint8,1) + label(string,4) + byte2(uint8,1)
    private const string NonScalarSkipContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "byte1": { "type": "uint8",  "size": 1 },
            "label": { "dependsOn": "byte1", "type": "string", "encoding": "ASCII", "size": 4 },
            "byte2": { "dependsOn": "label", "type": "uint8",  "size": 1 }
          }
        }
        """;

    // ================================================================
    // Unit-level tests (ParsedProperty directly) -- from Plan 01
    // ================================================================

    // -- GetUInt8 --

    [Test]
    public void GetUInt8_OnBinaryProperty_ReturnsCorrectByte()
    {
        var buffer = new byte[] { 0xAB };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.UInt8);
        prop.GetUInt8().Should().Be(0xAB);
    }

    // -- GetUInt16 --

    [Test]
    public void GetUInt16_LittleEndian_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    [Test]
    public void GetUInt16_BigEndian_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x00, 0x01 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 1, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    // -- GetUInt32 --

    [Test]
    public void GetUInt32_LittleEndian4Bytes_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(0x12345678u);
    }

    [Test]
    public void GetUInt32_BigEndian4Bytes_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 1, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(0x12345678u);
    }

    [Test]
    public void GetUInt32_BigEndian3Bytes_ZeroPads()
    {
        // [0xFF, 0xCF, 0xC7] -> 16764871 (zero-padded)
        var buffer = new byte[] { 0xFF, 0xCF, 0xC7 };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 1, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(16764871u);
    }

    // -- GetInt32 truncated --

    [Test]
    public void GetInt32_BigEndian3Bytes_SignExtends()
    {
        // [0xFF, 0xCF, 0xC7] -> -12345 (sign-extended)
        var buffer = new byte[] { 0xFF, 0xCF, 0xC7 };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 1, FieldTypes.Int32);
        prop.GetInt32().Should().Be(-12345);
    }

    [Test]
    public void GetInt32_LittleEndian3Bytes_SignExtends()
    {
        // -12345 in little-endian 3 bytes: 0xC7, 0xCF, 0xFF
        var buffer = new byte[] { 0xC7, 0xCF, 0xFF };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 0, FieldTypes.Int32);
        prop.GetInt32().Should().Be(-12345);
    }

    // -- Type strictness (unit) --

    [Test]
    public void GetInt32_OnUInt16Field_ThrowsInvalidOperationException()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);

        var act = () => prop.GetInt32();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetUInt16_OnUInt16Field_Succeeds()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    // -- GetBoolean (unit) --

    [Test]
    public void GetBoolean_NonZero_ReturnsTrue()
    {
        var buffer = new byte[] { 0x01 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.Boolean);
        prop.GetBoolean().Should().BeTrue();
    }

    [Test]
    public void GetBoolean_Zero_ReturnsFalse()
    {
        var buffer = new byte[] { 0x00 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.Boolean);
        prop.GetBoolean().Should().BeFalse();
    }

    [Test]
    public void GetBoolean_OnNonBooleanField_Throws()
    {
        var buffer = new byte[] { 0x01 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.UInt8);

        var act = () => prop.GetBoolean();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- GetDouble widening (unit) --

    [Test]
    public void GetDouble_OnFloat32Field_WidensCorrectly()
    {
        float expected = 3.14f;
        var buffer = new byte[4];
        BitConverter.TryWriteBytes(buffer, expected);
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.Float32);
        prop.GetDouble().Should().BeApproximately(expected, 0.001);
    }

    [Test]
    public void GetDouble_OnUInt32Field_Throws()
    {
        var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.UInt32);

        var act = () => prop.GetDouble();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- JSON format bypasses type check --

    [Test]
    public void GetInt32_JsonFormat_BypassesTypeCheck()
    {
        var buffer = "42"u8.ToArray();
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/test");
        prop.GetInt32().Should().Be(42);
    }

    // ================================================================
    // End-to-end tests: Load contract -> Parse payload -> GetXxx()
    // ================================================================

    // -- Helper to build a payload for the 22-byte LE/BE contract --

    private static byte[] BuildScalarPayload(
        short temperature, byte humidity, ushort voltage, int current,
        float power, double energy, bool active, bool bigEndian)
    {
        var payload = new byte[22];
        if (bigEndian)
        {
            BinaryPrimitives.WriteInt16BigEndian(payload.AsSpan(0, 2), temperature);
            payload[2] = humidity;
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(3, 2), voltage);
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(5, 4), current);
            BinaryPrimitives.WriteSingleBigEndian(payload.AsSpan(9, 4), power);
            BinaryPrimitives.WriteDoubleBigEndian(payload.AsSpan(13, 8), energy);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), temperature);
            payload[2] = humidity;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3, 2), voltage);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(5, 4), current);
            BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(9, 4), power);
            BinaryPrimitives.WriteDoubleLittleEndian(payload.AsSpan(13, 8), energy);
        }
        payload[21] = active ? (byte)0x01 : (byte)0x00;
        return payload;
    }

    // -- SCLR-01: Unsigned integers with correct endianness --

    [Test]
    public void Parse_LittleEndian_UInt8_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0xAB, 0, 0, 0f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["humidity"].GetUInt8().Should().Be(0xAB);
    }

    [Test]
    public void Parse_LittleEndian_UInt16_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 513, 0, 0f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["voltage"].GetUInt16().Should().Be(513);
    }

    [Test]
    public void Parse_BigEndian_UInt16_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(BigEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 513, 0, 0f, 0d, false, bigEndian: true);

        using var result = schema.Parse(payload)!.Value;

        result["voltage"].GetUInt16().Should().Be(513);
    }

    // -- SCLR-02: Signed integers with correct endianness --

    [Test]
    public void Parse_LittleEndian_Int32_ReturnsCorrectNegativeValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, -12345, 0f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["current"].GetInt32().Should().Be(-12345);
    }

    [Test]
    public void Parse_BigEndian_Int32_ReturnsCorrectNegativeValue()
    {
        var schema = BinaryContractSchema.Load(BigEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, -12345, 0f, 0d, false, bigEndian: true);

        using var result = schema.Parse(payload)!.Value;

        result["current"].GetInt32().Should().Be(-12345);
    }

    [Test]
    public void Parse_LittleEndian_Int32_ReturnsCorrectPositiveValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 12345, 0f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["current"].GetInt32().Should().Be(12345);
    }

    // -- SCLR-03: Float32 and Float64 with correct endianness --

    [Test]
    public void Parse_LittleEndian_Float32_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 3.14f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["power"].GetDouble().Should().BeApproximately(3.14f, 0.001);
    }

    [Test]
    public void Parse_LittleEndian_Float64_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 0f, 2.71828, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["energy"].GetDouble().Should().Be(2.71828);
    }

    [Test]
    public void Parse_BigEndian_Float32_ReturnsCorrectValue()
    {
        var schema = BinaryContractSchema.Load(BigEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 3.14f, 0d, false, bigEndian: true);

        using var result = schema.Parse(payload)!.Value;

        result["power"].GetDouble().Should().BeApproximately(3.14f, 0.001);
    }

    // -- SCLR-04: Boolean --

    [Test]
    public void Parse_Boolean_ZeroReturnsFalse()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 0f, 0d, false, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["active"].GetBoolean().Should().BeFalse();
    }

    [Test]
    public void Parse_Boolean_NonZeroReturnsTrue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 0f, 0d, true, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["active"].GetBoolean().Should().BeTrue();
    }

    [Test]
    public void Parse_Boolean_HighValueReturnsTrue()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(0, 0, 0, 0, 0f, 0d, false, bigEndian: false);
        payload[21] = 0xFF; // set active to 0xFF directly

        using var result = schema.Parse(payload)!.Value;

        result["active"].GetBoolean().Should().BeTrue();
    }

    // -- SCLR-05: Truncated signed int32 with sign extension --

    [Test]
    public void Parse_TruncatedSigned_BigEndian_NegativeValue_SignExtends()
    {
        var schema = BinaryContractSchema.Load(TruncatedContractJson)!;
        // [0xFF, 0xCF, 0xC7] for truncatedSigned, [0x00, 0x00, 0x00] for truncatedUnsigned
        var payload = new byte[] { 0xFF, 0xCF, 0xC7, 0x00, 0x00, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        result["truncatedSigned"].GetInt32().Should().Be(-12345);
    }

    [Test]
    public void Parse_TruncatedSigned_BigEndian_PositiveValue_ZeroPads()
    {
        var schema = BinaryContractSchema.Load(TruncatedContractJson)!;
        // [0x01, 0x00, 0x00] = 65536 as signed 3-byte big-endian
        var payload = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        result["truncatedSigned"].GetInt32().Should().Be(65536);
    }

    // -- SCLR-06: Truncated unsigned uint32 with zero padding --

    [Test]
    public void Parse_TruncatedUnsigned_BigEndian_ZeroPads()
    {
        var schema = BinaryContractSchema.Load(TruncatedContractJson)!;
        // [0x00, 0x00, 0x00] for truncatedSigned, [0xFF, 0xCF, 0xC7] for truncatedUnsigned at offset 3
        var payload = new byte[] { 0x00, 0x00, 0x00, 0xFF, 0xCF, 0xC7 };

        using var result = schema.Parse(payload)!.Value;

        result["truncatedUnsigned"].GetUInt32().Should().Be(16764871u);
    }

    // -- CORE-04: Parse returns null for short payload --

    [Test]
    public void Parse_PayloadTooShort_ReturnsNull()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var shortPayload = new byte[10]; // contract needs 22 bytes

        var result = schema.Parse(shortPayload);

        result.Should().BeNull();
    }

    [Test]
    public void Parse_PayloadExactSize_ReturnsParsedResult()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = new byte[22]; // exact size

        var result = schema.Parse(payload);

        result.Should().NotBeNull();
    }

    [Test]
    public void Parse_SpanOverload_ReturnsSameResult()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(-100, 0xAB, 513, -12345, 3.14f, 2.71828, true, bigEndian: false);

        using var arrayResult = schema.Parse(payload)!.Value;
        using var spanResult = schema.Parse((ReadOnlySpan<byte>)payload)!.Value;

        spanResult["humidity"].GetUInt8().Should().Be(arrayResult["humidity"].GetUInt8());
        spanResult["voltage"].GetUInt16().Should().Be(arrayResult["voltage"].GetUInt16());
        spanResult["current"].GetInt32().Should().Be(arrayResult["current"].GetInt32());
        spanResult["active"].GetBoolean().Should().Be(arrayResult["active"].GetBoolean());
    }

    // -- CORE-05: Zero-allocation verification (structural) --

    [Test]
    public void Parse_ReturnsDisposableParseResult()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = new byte[22];

        var result = schema.Parse(payload);

        result.Should().NotBeNull();
        // ParseResult implements IDisposable -- using pattern works without exception
        var act = () =>
        {
            using var r = result!.Value;
            // access a value to prove it works inside using scope
            _ = r["humidity"].GetUInt8();
        };
        act.Should().NotThrow();
    }

    // -- Type strictness (end-to-end) --

    [Test]
    public void Parse_GetInt32_OnUInt16Field_ThrowsInvalidOperationException_E2E()
    {
        var schema = BinaryContractSchema.Load(TypeStrictnessContractJson)!;
        var payload = new byte[] { 0x39, 0x05 }; // 1337 in LE

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["myUint16"].GetInt32();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Parse_GetUInt16_OnUInt16Field_ReturnsCorrectValue_E2E()
    {
        var schema = BinaryContractSchema.Load(TypeStrictnessContractJson)!;
        var payload = new byte[] { 0x39, 0x05 }; // 1337 in LE

        using var result = schema.Parse(payload)!.Value;

        result["myUint16"].GetUInt16().Should().Be(1337);
    }

    [Test]
    public void Parse_GetDouble_OnBooleanField_ThrowsInvalidOperationException()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = new byte[22];
        payload[21] = 0x01;

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["active"].GetDouble();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- Non-scalar field handling --

    [Test]
    public void Parse_NonScalarFieldSlot_ReturnsEmptyParsedProperty()
    {
        var schema = BinaryContractSchema.Load(NonScalarSkipContractJson)!;
        // 6-byte payload: byte1=0x42 at 0, label=4 bytes at 1, byte2=0x99 at 5
        var payload = new byte[] { 0x42, 0x41, 0x42, 0x43, 0x44, 0x99 };

        using var result = schema.Parse(payload)!.Value;

        // byte1 should parse correctly as scalar
        result["byte1"].GetUInt8().Should().Be(0x42);

        // label is a string field, now parsed as leaf type (Phase 4)
        result["label"].HasValue.Should().BeTrue();
        result["label"].GetString().Should().Be("ABCD");

        // byte2 should parse correctly at correct offset (precomputed by chain resolver)
        result["byte2"].GetUInt8().Should().Be(0x99);
    }

    // -- Multi-field end-to-end: all scalar types in one parse --

    [Test]
    public void Parse_AllScalarTypes_LittleEndian_EndToEnd()
    {
        var schema = BinaryContractSchema.Load(LittleEndianContractJson)!;
        var payload = BuildScalarPayload(-100, 0xAB, 513, -12345, 3.14f, 2.71828, true, bigEndian: false);

        using var result = schema.Parse(payload)!.Value;

        result["humidity"].GetUInt8().Should().Be(0xAB);
        result["voltage"].GetUInt16().Should().Be(513);
        result["current"].GetInt32().Should().Be(-12345);
        result["power"].GetDouble().Should().BeApproximately(3.14f, 0.001);
        result["energy"].GetDouble().Should().Be(2.71828);
        result["active"].GetBoolean().Should().BeTrue();
    }
}
