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
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

/// <summary>
/// Tests targeting specific coverage gaps across BinaryContractSchema, ParsedProperty,
/// BinaryContractLoader, BinaryChainResolver, and BinaryFieldValidator.
/// </summary>
[TestFixture]
internal sealed class CoverageGapTests
{
    // ================================================================
    // BinaryContractSchema — Pass 2 (dynamic offset) paths
    // ================================================================

    // Semi-dynamic struct array: exercises Pass 2 struct parsing with validation
    private const string SemiDynamicStructArrayContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "count": { "type": "uint8", "size": 1 },
            "records": {
              "dependsOn": "count",
              "type": "array",
              "count": "count",
              "element": {
                "type": "struct",
                "size": 4,
                "fields": {
                  "value": { "type": "int16", "size": 2, "validation": { "min": -100, "max": 100 } },
                  "tag":   { "dependsOn": "value", "type": "string", "encoding": "ASCII", "size": 2, "validation": { "minLength": 1 } }
                }
              }
            }
          }
        }
        """;

    [Test]
    public void Parse_SemiDynamicStructArray_Pass2Path_ParsesCorrectly()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicStructArrayContractJson)!;
        // count=2, record[0]={value=42, tag="AB"}, record[1]={value=-5, tag="CD"}
        var payload = new byte[9];
        payload[0] = 2; // count
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(1, 2), 42);
        payload[3] = (byte)'A'; payload[4] = (byte)'B';
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(5, 2), -5);
        payload[7] = (byte)'C'; payload[8] = (byte)'D';

        using var result = schema.Parse(payload)!.Value;

        result["records/0/value"].GetInt64().Should().Be(42);
        result["records/0/tag"].GetString().Should().Be("AB");
        result["records/1/value"].GetInt64().Should().Be(-5);
        result["records/1/tag"].GetString().Should().Be("CD");
    }

    [Test]
    public void Parse_SemiDynamicStructArray_ValidationOnSubFields()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicStructArrayContractJson)!;
        // count=1, record[0]={value=200 (above max 100), tag="AB" (valid)}
        var payload = new byte[5];
        payload[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(1, 2), 200);
        payload[3] = (byte)'A'; payload[4] = (byte)'B';

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Contain("records/0/value");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
    }

    [Test]
    public void Parse_SemiDynamicStructArray_TruncatedPayload_SkipsOutOfBoundsSubFields()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicStructArrayContractJson)!;
        // count=2 but payload only fits 1 full element + partial second element
        var payload = new byte[7]; // 1 (count) + 4 (elem0) + 2 (partial elem1, missing tag)
        payload[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(1, 2), 10);
        payload[3] = (byte)'O'; payload[4] = (byte)'K';
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(5, 2), 20);
        // tag bytes for elem1 are out of bounds

        // Should not throw — graceful degradation clamps count
        using var result = schema.Parse(payload)!.Value;
        result["records/0/value"].GetInt64().Should().Be(10);
    }

    // Semi-dynamic scalar array with validation — Pass 2 scalar validation path
    private const string SemiDynamicScalarValidationContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "readings": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint16", "size": 2 },
              "validation": { "min": 0, "max": 1000 }
            }
          }
        }
        """;

    [Test]
    public void Parse_SemiDynamicScalarArray_Pass2Validation()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicScalarValidationContractJson)!;
        // n=2, readings=[500, 2000 (above max)]
        var payload = new byte[5];
        payload[0] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1, 2), 500);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3, 2), 2000);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Be("/readings/1");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
    }

    [Test]
    public void Parse_SemiDynamicScalarArray_TruncatedPayload_BreaksEarly()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicScalarValidationContractJson)!;
        // n=3 but payload only has room for 1 element
        var payload = new byte[3]; // 1 (count) + 2 (one uint16)
        payload[0] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1, 2), 42);

        using var result = schema.Parse(payload)!.Value;
        // Should parse 1 element, skip the other 2
        result["readings/0"].GetUInt16().Should().Be(42);
    }

    // Pass 2 string field
    private const string SemiDynamicWithStringAfterContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "items": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "label": {
              "dependsOn": "items",
              "type": "string", "encoding": "ASCII", "size": 4,
              "validation": { "pattern": "^[A-Z]+$" }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_StringFieldWithValidation()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithStringAfterContractJson)!;
        // n=2, items=[1,2], label="fail" (lowercase, pattern fails)
        var payload = new byte[7];
        payload[0] = 2;
        payload[1] = 1; payload[2] = 2;
        Encoding.ASCII.GetBytes("fail", payload.AsSpan(3, 4));

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Be("/label");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
    }

    [Test]
    public void Parse_Pass2_StringFieldValid()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithStringAfterContractJson)!;
        var payload = new byte[7];
        payload[0] = 2;
        payload[1] = 1; payload[2] = 2;
        Encoding.ASCII.GetBytes("OKAY", payload.AsSpan(3, 4));

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result["label"].GetString().Should().Be("OKAY");
    }

    // Pass 2 enum field
    private const string SemiDynamicWithEnumAfterContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "items": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "status": {
              "dependsOn": "items",
              "type": "enum", "size": 1,
              "values": { "0": "off", "1": "on" }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_EnumField()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithEnumAfterContractJson)!;
        var payload = new byte[4];
        payload[0] = 2;
        payload[1] = 10; payload[2] = 20;
        payload[3] = 1; // status = "on"

        using var result = schema.Parse(payload)!.Value;

        result["status"].GetUInt8().Should().Be(1);
        result["statuss"].GetString().Should().Be("on");
    }

    // Pass 2 bits field
    private const string SemiDynamicWithBitsAfterContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "items": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "flags": {
              "dependsOn": "items",
              "type": "bits", "size": 1,
              "fields": {
                "active": { "bit": 0, "bits": 1, "type": "boolean" },
                "level":  { "bit": 1, "bits": 3, "type": "uint8" }
              }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_BitsField()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithBitsAfterContractJson)!;
        var payload = new byte[3];
        payload[0] = 1; // n=1
        payload[1] = 42; // items[0]
        payload[2] = 0b00000111; // active=1, level=3

        using var result = schema.Parse(payload)!.Value;

        result["flags/active"].GetBoolean().Should().BeTrue();
        result["flags/level"].GetUInt8().Should().Be(3);
    }

    // Pass 2 padding field
    private const string SemiDynamicWithPaddingAfterContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "items": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "gap": {
              "dependsOn": "items",
              "type": "padding", "size": 2
            },
            "trailer": {
              "dependsOn": "gap",
              "type": "uint16", "size": 2
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_PaddingField_SkippedCorrectly()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithPaddingAfterContractJson)!;
        var payload = new byte[6];
        payload[0] = 1; // n=1
        payload[1] = 99; // items[0]
        payload[2] = 0xFF; payload[3] = 0xFF; // padding
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 1234);

        using var result = schema.Parse(payload)!.Value;

        result["trailer"].GetUInt16().Should().Be(1234);
    }

    // Pass 2 scalar numeric with validation
    private const string SemiDynamicWithScalarValidationAfterContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "items": {
              "dependsOn": "n",
              "type": "array",
              "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "temp": {
              "dependsOn": "items",
              "type": "int16", "size": 2,
              "validation": { "min": -40, "max": 85 }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_ScalarNumericWithValidation_Fails()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithScalarValidationAfterContractJson)!;
        var payload = new byte[4];
        payload[0] = 1;
        payload[1] = 0;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2, 2), 100); // above max 85

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Be("/temp");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
    }

    [Test]
    public void Parse_Pass2_ScalarNumericWithValidation_Passes()
    {
        var schema = BinaryContractSchema.Load(SemiDynamicWithScalarValidationAfterContractJson)!;
        var payload = new byte[4];
        payload[0] = 1;
        payload[1] = 0;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2, 2), 25);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result["temp"].GetInt64().Should().Be(25);
    }

    // ================================================================
    // BinaryContractSchema — Span overload
    // ================================================================

    [Test]
    public void Parse_SpanOverload_ReturnsNull_WhenPayloadTooShort()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "value": { "type": "uint32", "size": 4 }
              }
            }
            """)!;

        ReadOnlySpan<byte> shortPayload = new byte[] { 0x01, 0x02 };
        var result = schema.Parse(shortPayload);

        result.Should().BeNull();
    }

    // ================================================================
    // BinaryContractSchema — TryLoad failure paths
    // ================================================================

    [Test]
    public void TryLoad_InvalidJson_ReturnsFalse()
    {
        var result = BinaryContractSchema.TryLoad("not json at all"u8, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    [Test]
    public void TryLoad_ValidationFailure_ReturnsFalse()
    {
        // Two root fields (no dependsOn) — fails validation
        var result = BinaryContractSchema.TryLoad("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "a": { "type": "uint8", "size": 1 },
                "b": { "type": "uint8", "size": 1 }
              }
            }
            """u8, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    [Test]
    public void Load_String_InvalidJson_ReturnsNull()
    {
        var schema = BinaryContractSchema.Load("{}");

        schema.Should().BeNull();
    }

    // ================================================================
    // ParsedProperty — type strictness throws
    // ================================================================

    [Test]
    public void GetUInt16_OnUInt8Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt16();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetUInt32_OnUInt8Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt32();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetInt32_OnUInt16Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint16", "size": 2 } }
            }
            """)!;
        var payload = new byte[] { 0x01, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetInt32();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetInt64_OnUInt16Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint16", "size": 2 } }
            }
            """)!;
        var payload = new byte[] { 0x01, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetInt64();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetDouble_OnUInt8Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetDouble();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetBoolean_OnUInt8Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetBoolean();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetString_OnUInt8Field_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetString();
        act.Should().Throw<InvalidOperationException>();
    }

    // ================================================================
    // ParsedProperty — GetInt64 on Int8 and Int16 fields (allowed)
    // ================================================================

    [Test]
    public void GetInt64_OnInt8Field_Succeeds()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int8", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 0xFE }; // -2 as signed byte

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-2);
    }

    [Test]
    public void GetInt64_OnInt16Field_Succeeds()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int16", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(payload, -300);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-300);
    }

    // ================================================================
    // ParsedProperty — Enum GetString with unmapped value
    // ================================================================

    [Test]
    public void GetString_Enum_UnmappedValue_ReturnsNumericString()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "mode": { "type": "enum", "size": 1, "values": { "0": "off", "1": "on" } }
              }
            }
            """)!;
        var payload = new byte[] { 99 }; // not in values map

        using var result = schema.Parse(payload)!.Value;

        result["modes"].GetString().Should().Be("99");
    }

    // ================================================================
    // ParsedProperty — big-endian UInt16 and UInt32 paths
    // ================================================================

    [Test]
    public void GetUInt16_BigEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint16", "size": 2 } }
            }
            """)!;
        var payload = new byte[] { 0x01, 0x02 }; // 258 in big endian

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt16().Should().Be(258);
    }

    [Test]
    public void GetUInt32_BigEndian_3Bytes_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint32", "size": 3 } }
            }
            """)!;
        var payload = new byte[] { 0x01, 0x02, 0x03 }; // 0x010203 = 66051

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt32().Should().Be(66051u);
    }

    [Test]
    public void GetInt32_BigEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, -12345);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt32().Should().Be(-12345);
    }

    [Test]
    public void GetInt64_BigEndian_8Bytes_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, -99999);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-99999);
    }

    [Test]
    public void GetDouble_BigEndian_Float32_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "float32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(payload, 3.14f);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetDouble().Should().BeApproximately(3.14, 0.001);
    }

    [Test]
    public void GetDouble_BigEndian_Float64_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "float64", "size": 8 } }
            }
            """)!;
        var payload = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(payload, 2.718281828);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetDouble().Should().BeApproximately(2.718281828, 0.000001);
    }

    // ================================================================
    // ParsedProperty — string trim modes via binary parse
    // ================================================================

    [Test]
    public void GetString_TrimStart_RemovesLeadingNulls()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "label": { "type": "string", "encoding": "ASCII", "size": 6, "mode": "trimStart" }
              }
            }
            """)!;
        var payload = new byte[] { 0x00, 0x00, (byte)'H', (byte)'i', 0x00, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        // trimStart removes leading nulls, trailing nulls remain
        result["label"].GetString().Should().Be("Hi\0\0");
    }

    [Test]
    public void GetString_Trim_RemovesBothLeadingAndTrailing()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "label": { "type": "string", "encoding": "UTF-8", "size": 8, "mode": "trim" }
              }
            }
            """)!;
        var payload = new byte[] { 0x00, 0x00, (byte)'O', (byte)'K', 0x20, 0x09, 0x0A, 0x0D };

        using var result = schema.Parse(payload)!.Value;

        // trim removes leading nulls and trailing whitespace (space, tab, newline, CR)
        result["label"].GetString().Should().Be("OK");
    }

    [Test]
    public void GetString_Plain_KeepsAllBytes()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "raw": { "type": "string", "encoding": "ASCII", "size": 4, "mode": "plain" }
              }
            }
            """)!;
        var payload = new byte[] { 0x00, (byte)'A', (byte)'B', 0x00 };

        using var result = schema.Parse(payload)!.Value;

        result["raw"].GetString().Should().Be("\0AB\0");
    }

    // ================================================================
    // BinaryContractSchema — Fixed array with fixed count in Pass 1
    // ================================================================

    [Test]
    public void Parse_FixedArrayWithValidation_Pass1()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "readings": {
                  "type": "array", "count": 3,
                  "element": { "type": "uint16", "size": 2 },
                  "validation": { "min": 0, "max": 500 }
                }
              }
            }
            """)!;
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 999); // above max
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 200);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Be("/readings/1");
    }

    // ================================================================
    // BinaryContractSchema — Fixed struct array with string sub-field validation
    // ================================================================

    [Test]
    public void Parse_FixedStructArray_StringValidationOnSubField()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 2,
                  "element": {
                    "type": "struct", "size": 5,
                    "fields": {
                      "id": { "type": "uint8", "size": 1 },
                      "name": {
                        "dependsOn": "id",
                        "type": "string", "encoding": "ASCII", "size": 4,
                        "validation": { "pattern": "^[A-Z]+$" }
                      }
                    }
                  }
                }
              }
            }
            """)!;
        var payload = new byte[10];
        payload[0] = 1;
        Encoding.ASCII.GetBytes("GOOD", payload.AsSpan(1, 4));
        payload[5] = 2;
        Encoding.ASCII.GetBytes("bad!", payload.AsSpan(6, 4)); // fails pattern

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Contain("items/1/name");
    }

    // ================================================================
    // BinaryChainResolver — edge cases
    // ================================================================

    [Test]
    public void ChainResolver_EmptyFields_ReturnsEmptyArray()
    {
        var result = BinaryChainResolver.Resolve(
            new Dictionary<string, BinaryContractNode>(),
            "little");

        result.Should().BeEmpty();
    }

    [Test]
    public void ChainResolver_NoRoot_ReturnsEmptyArray()
    {
        // All fields have dependsOn — no root
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["a"] = new() { Name = "a", Type = "uint8", Size = 1, DependsOn = "b" },
            ["b"] = new() { Name = "b", Type = "uint8", Size = 1, DependsOn = "a" }
        };

        var result = BinaryChainResolver.Resolve(fields, "little");

        result.Should().BeEmpty();
    }

    // ================================================================
    // BinaryContractSchema — Metadata properties
    // ================================================================

    [Test]
    public void Load_ContractMetadata_PopulatesIdNameVersion()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "id": "sensor/temp",
              "name": "temperature",
              "version": "2.1.0",
              "endianness": "little",
              "fields": {
                "value": { "type": "uint8", "size": 1 }
              }
            }
            """)!;

        schema.Id.Should().Be("sensor/temp");
        schema.Name.Should().Be("temperature");
        schema.Version.Should().Be("2.1.0");
    }

    // ================================================================
    // BinaryContractSchema — Pass 2 bounds check for out-of-bounds scalar field
    // ================================================================

    [Test]
    public void Parse_Pass2_ScalarFieldOutOfBounds_SkippedGracefully()
    {
        // A field after a semi-dynamic array that lands past the payload end
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "n": { "type": "uint8", "size": 1 },
                "items": {
                  "dependsOn": "n",
                  "type": "array",
                  "count": "n",
                  "element": { "type": "uint8", "size": 1 }
                },
                "trailer": {
                  "dependsOn": "items",
                  "type": "uint32", "size": 4
                }
              }
            }
            """)!;
        // n=3, items=[1,2,3], no room for trailer
        var payload = new byte[] { 3, 1, 2, 3 };

        using var result = schema.Parse(payload)!.Value;

        // Should parse without crashing, trailer field not available
        result["items/0"].GetUInt8().Should().Be(1);
        result["items/2"].GetUInt8().Should().Be(3);
    }

    // ================================================================
    // BinaryContractSchema — Semi-dynamic struct array with string validation via x-error
    // ================================================================

    [Test]
    public void Parse_SemiDynamicStructArray_XError_OnSubField()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "n": { "type": "uint8", "size": 1 },
                "items": {
                  "dependsOn": "n",
                  "type": "array",
                  "count": "n",
                  "element": {
                    "type": "struct",
                    "size": 3,
                    "fields": {
                      "val": {
                        "type": "uint8", "size": 1,
                        "validation": { "max": 10 },
                        "x-error": { "code": "VAL_HIGH", "detail": "Value exceeds limit" }
                      },
                      "tag": {
                        "dependsOn": "val",
                        "type": "string", "encoding": "ASCII", "size": 2
                      }
                    }
                  }
                }
              }
            }
            """)!;
        var payload = new byte[] { 1, 50, (byte)'A', (byte)'B' };

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Be("Value exceeds limit");
        result.Errors[0].ErrorInfo!.Value.Code.Should().Be("VAL_HIGH");
    }
}
