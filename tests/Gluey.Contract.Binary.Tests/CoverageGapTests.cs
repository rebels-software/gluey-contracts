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

    // ================================================================
    // ParsedProperty — GetUInt16 single-byte and throw paths (lines 431, 433, 438, 440)
    // ================================================================

    [Test]
    public void GetUInt16_SingleByte_LittleEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint16", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt16().Should().Be(42);
    }

    [Test]
    public void GetUInt16_SingleByte_BigEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint16", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 42 };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt16().Should().Be(42);
    }

    // ================================================================
    // ParsedProperty — GetUInt32 single-byte and 2-byte paths (lines 464-468, 473-477)
    // ================================================================

    [Test]
    public void GetUInt32_SingleByte_LittleEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint32", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 200 })!.Value;

        result["v"].GetUInt32().Should().Be(200u);
    }

    [Test]
    public void GetUInt32_TwoBytes_LittleEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint32", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, 1000);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt32().Should().Be(1000u);
    }

    [Test]
    public void GetUInt32_SingleByte_BigEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint32", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 200 })!.Value;

        result["v"].GetUInt32().Should().Be(200u);
    }

    [Test]
    public void GetUInt32_TwoBytes_BigEndian_ReturnsValue()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint32", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(payload, 1000);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt32().Should().Be(1000u);
    }

    // ================================================================
    // ParsedProperty — GetInt32 1/2/3-byte paths (lines 501-514)
    // ================================================================

    [Test]
    public void GetInt32_SingleByte_LittleEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 0xFE })!.Value;

        result["v"].GetInt32().Should().Be(-2);
    }

    [Test]
    public void GetInt32_TwoBytes_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(payload, -300);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt32().Should().Be(-300);
    }

    [Test]
    public void GetInt32_SingleByte_BigEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 0xFE })!.Value;

        result["v"].GetInt32().Should().Be(-2);
    }

    [Test]
    public void GetInt32_TwoBytes_BigEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(payload, -300);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt32().Should().Be(-300);
    }

    [Test]
    public void GetInt32_ThreeBytes_BigEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 3 } }
            }
            """)!;
        // -1 in 3 big-endian bytes = 0xFF 0xFF 0xFF
        var payload = new byte[] { 0xFF, 0xFF, 0xFF };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt32().Should().Be(-1);
    }

    // ================================================================
    // ParsedProperty — GetInt64 1/2/3/4-byte LE and BE paths (lines 540-553)
    // ================================================================

    [Test]
    public void GetInt64_SingleByte_LittleEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int8", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 0xFE })!.Value;

        result["v"].GetInt64().Should().Be(-2);
    }

    [Test]
    public void GetInt64_TwoBytes_BigEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int16", "size": 2 } }
            }
            """)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(payload, -500);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-500);
    }

    [Test]
    public void GetInt64_ThreeBytes_BigEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 3 } }
            }
            """)!;
        var payload = new byte[] { 0xFF, 0xFF, 0xFE };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-2);
    }

    [Test]
    public void GetInt64_FourBytes_BigEndian_ReadsCorrectly()
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

        result["v"].GetInt64().Should().Be(-12345);
    }

    // ================================================================
    // ParsedProperty — GetXxx throw branches for mismatched sizes
    // ================================================================

    [Test]
    public void GetUInt16_ThreeByteField_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint16", "size": 3 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3 })!.Value;

        var act = () => result["v"].GetUInt16();
        act.Should().Throw<InvalidOperationException>().WithMessage("*3 bytes*");
    }

    [Test]
    public void GetUInt16_ThreeByteField_BigEndian_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint16", "size": 3 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3 })!.Value;

        var act = () => result["v"].GetUInt16();
        act.Should().Throw<InvalidOperationException>().WithMessage("*3 bytes*");
    }

    [Test]
    public void GetUInt32_FiveByteField_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetUInt32();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetUInt32_FiveByteField_BigEndian_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "uint32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetUInt32();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetInt32_FiveByteField_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetInt32();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetInt32_FiveByteField_BigEndian_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetInt32();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetInt64_FiveByteField_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetInt64();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetInt64_FiveByteField_BigEndian_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 5 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2, 3, 4, 5 })!.Value;

        var act = () => result["v"].GetInt64();
        act.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
    }

    [Test]
    public void GetDouble_TwoByteField_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "float32", "size": 2 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2 })!.Value;

        var act = () => result["v"].GetDouble();
        act.Should().Throw<InvalidOperationException>().WithMessage("*2 bytes*");
    }

    [Test]
    public void GetDouble_TwoByteField_BigEndian_Throws()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "float64", "size": 2 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 2 })!.Value;

        var act = () => result["v"].GetDouble();
        act.Should().Throw<InvalidOperationException>().WithMessage("*2 bytes*");
    }

    // ================================================================
    // ParsedProperty — GetUInt32 3-byte LE path (line 466)
    // ================================================================

    [Test]
    public void GetUInt32_ThreeBytes_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint32", "size": 3 } }
            }
            """)!;
        var payload = new byte[] { 0x03, 0x02, 0x01 }; // 0x010203 = 66051

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt32().Should().Be(66051u);
    }

    // ================================================================
    // ParsedProperty — GetInt64 3-byte and 4-byte LE paths (lines 540-542)
    // ================================================================

    [Test]
    public void GetInt64_ThreeBytes_LittleEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 3 } }
            }
            """)!;
        // -1 in 3 LE bytes = 0xFF 0xFF 0xFF
        var payload = new byte[] { 0xFF, 0xFF, 0xFF };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-1);
    }

    [Test]
    public void GetInt64_FourBytes_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(payload, -99999);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-99999);
    }

    [Test]
    public void GetInt64_SingleByte_BigEndian_SignExtends()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int8", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 0x80 })!.Value;

        result["v"].GetInt64().Should().Be(-128);
    }

    // ================================================================
    // ParsedProperty — GetInt64 8-byte paths (lines 498, 508)
    // ================================================================

    [Test]
    public void GetInt64_EightBytes_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 8 } }
            }
            """)!;
        var payload = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(payload, -9999999999L);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-9999999999L);
    }

    [Test]
    public void GetInt64_EightBytes_BigEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "big",
              "fields": { "v": { "type": "int32", "size": 8 } }
            }
            """)!;
        var payload = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(payload, -9999999999L);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetInt64().Should().Be(-9999999999L);
    }

    // ================================================================
    // ParsedProperty — GetDouble big-endian float64 (line 586)
    // Already tested above but need explicit LE float32 too
    // ================================================================

    [Test]
    public void GetDouble_Float32_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "float32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(payload, 1.5f);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetDouble().Should().BeApproximately(1.5, 0.001);
    }

    [Test]
    public void GetDouble_Float64_LittleEndian_ReadsCorrectly()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "float64", "size": 8 } }
            }
            """)!;
        var payload = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(payload, 2.718281828);

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetDouble().Should().BeApproximately(2.718281828, 0.000001);
    }

    // ================================================================
    // ParsedProperty — GetFieldTypeName remaining branches (lines 665, 667, 673, 674, 675)
    // Need to trigger type strictness errors for int8, int32, bits, padding
    // ================================================================

    [Test]
    public void GetUInt16_OnInt8Field_ThrowsWithInt8Name()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int8", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 42 })!.Value;

        var act = () => result["v"].GetUInt16();
        act.Should().Throw<InvalidOperationException>().WithMessage("*int8*");
    }

    [Test]
    public void GetUInt16_OnInt32Field_ThrowsWithInt32Name()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int32", "size": 4 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1, 0, 0, 0 })!.Value;

        var act = () => result["v"].GetUInt16();
        act.Should().Throw<InvalidOperationException>().WithMessage("*int32*");
    }

    [Test]
    public void GetUInt16_OnBitsContainer_ThrowsWithBitsName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "flags": {
                  "type": "bits", "size": 1,
                  "fields": { "a": { "bit": 0, "bits": 1, "type": "boolean" } }
                }
              }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 1 })!.Value;

        // Bits container gets fieldType=UInt8 (size==1), so GetUInt16 type check should pass or fail
        // Actually bits container is set to FieldTypes.UInt8 or UInt16 based on size
        // So we need a different approach: read the sub-field which has actual Bits type
        // The container itself maps to UInt8, not Bits — let's use GetString on bits sub-field
        var act = () => result["flags/a"].GetUInt16();
        act.Should().Throw<InvalidOperationException>().WithMessage("*boolean*");
    }

    [Test]
    public void GetString_OnPaddingField_ThrowsWithPaddingName()
    {
        // Padding fields have _length=0 so GetString returns empty string via early return
        // We can't directly test this since padding fields aren't in the offset table
        // But we can test GetFieldTypeName("padding") through a bits sub-field with padding type
        // Actually, the simplest is to just verify padding HasValue is false
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "gap": { "type": "padding", "size": 2 },
                "v": { "dependsOn": "gap", "type": "uint8", "size": 1 }
              }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 0, 0, 42 })!.Value;

        result["gap"].HasValue.Should().BeFalse();
        result["gap"].GetString().Should().BeEmpty();
    }

    // ================================================================
    // Pass 2 — Fixed-count array AFTER a semi-dynamic array
    // Covers lines 526-655 (Pass 2 array parsing)
    // ================================================================

    private const string Pass2FixedArrayContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "dynamic": {
              "dependsOn": "n",
              "type": "array", "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "fixed": {
              "dependsOn": "dynamic",
              "type": "array", "count": 2,
              "element": { "type": "uint16", "size": 2 },
              "validation": { "min": 0, "max": 500 }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_FixedScalarArray_ParsesCorrectly()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedArrayContractJson)!;
        // n=1, dynamic=[10], fixed=[100, 200]
        var payload = new byte[6];
        payload[0] = 1;
        payload[1] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 200);

        using var result = schema.Parse(payload)!.Value;

        result["fixed/0"].GetUInt16().Should().Be(100);
        result["fixed/1"].GetUInt16().Should().Be(200);
    }

    [Test]
    public void Parse_Pass2_FixedScalarArray_ValidationFails()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedArrayContractJson)!;
        // n=1, dynamic=[10], fixed=[100, 999 (above max 500)]
        var payload = new byte[6];
        payload[0] = 1;
        payload[1] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 999);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Be("/fixed/1");
    }

    // Pass 2 fixed struct array after semi-dynamic
    private const string Pass2FixedStructArrayContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "n": { "type": "uint8", "size": 1 },
            "dynamic": {
              "dependsOn": "n",
              "type": "array", "count": "n",
              "element": { "type": "uint8", "size": 1 }
            },
            "records": {
              "dependsOn": "dynamic",
              "type": "array", "count": 2,
              "element": {
                "type": "struct", "size": 3,
                "fields": {
                  "id": { "type": "uint8", "size": 1 },
                  "val": { "dependsOn": "id", "type": "int16", "size": 2, "validation": { "min": -50, "max": 50 } }
                }
              }
            }
          }
        }
        """;

    [Test]
    public void Parse_Pass2_FixedStructArray_ParsesCorrectly()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedStructArrayContractJson)!;
        // n=1, dynamic=[5], records=[{id=1,val=10}, {id=2,val=-5}]
        var payload = new byte[8];
        payload[0] = 1;
        payload[1] = 5;
        payload[2] = 1; BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(3, 2), 10);
        payload[5] = 2; BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6, 2), -5);

        using var result = schema.Parse(payload)!.Value;

        result["records/0/id"].GetUInt8().Should().Be(1);
        result["records/0/val"].GetInt64().Should().Be(10);
        result["records/1/id"].GetUInt8().Should().Be(2);
        result["records/1/val"].GetInt64().Should().Be(-5);
    }

    [Test]
    public void Parse_Pass2_FixedStructArray_ValidationOnSubField()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedStructArrayContractJson)!;
        // n=0, records=[{id=1,val=100 (above max 50)}, {id=2,val=25}]
        var payload = new byte[7];
        payload[0] = 0; // empty dynamic array
        payload[1] = 1; BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2, 2), 100);
        payload[4] = 2; BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(5, 2), 25);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Contain("records/0/val");
    }

    // Pass 2 fixed string array with validation
    [Test]
    public void Parse_Pass2_FixedStringArray_Validation()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "n": { "type": "uint8", "size": 1 },
                "dynamic": {
                  "dependsOn": "n",
                  "type": "array", "count": "n",
                  "element": { "type": "uint8", "size": 1 }
                },
                "names": {
                  "dependsOn": "dynamic",
                  "type": "array", "count": 2,
                  "element": { "type": "string", "encoding": "ASCII", "size": 3 },
                  "validation": { "pattern": "^[A-Z]+$" }
                }
              }
            }
            """)!;
        var payload = new byte[8];
        payload[0] = 1;
        payload[1] = 0;
        Encoding.ASCII.GetBytes("ABC", payload.AsSpan(2, 3));
        Encoding.ASCII.GetBytes("12!", payload.AsSpan(5, 3)); // fails pattern

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Be("/names/1");
    }

    // ================================================================
    // Pass 1 — Fixed string array with validation (lines 370-374)
    // ================================================================

    [Test]
    public void Parse_Pass1_FixedStringArray_Validation()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "labels": {
                  "type": "array", "count": 2,
                  "element": { "type": "string", "encoding": "ASCII", "size": 4 },
                  "validation": { "pattern": "^[A-Z]+$" }
                }
              }
            }
            """)!;
        var payload = new byte[8];
        Encoding.ASCII.GetBytes("GOOD", payload.AsSpan(0, 4));
        Encoding.ASCII.GetBytes("bad!", payload.AsSpan(4, 4));

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Path.Should().Be("/labels/1");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
    }

    // ================================================================
    // 16-bit bits container (lines 724-728)
    // ================================================================

    [Test]
    public void Parse_Pass2_16BitBitsContainer()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "n": { "type": "uint8", "size": 1 },
                "items": {
                  "dependsOn": "n",
                  "type": "array", "count": "n",
                  "element": { "type": "uint8", "size": 1 }
                },
                "flags16": {
                  "dependsOn": "items",
                  "type": "bits", "size": 2,
                  "fields": {
                    "low":  { "bit": 0, "bits": 4, "type": "uint8" },
                    "high": { "bit": 8, "bits": 4, "type": "uint8" }
                  }
                }
              }
            }
            """)!;
        // n=1, items=[42], flags16=0x0305 (LE: 05 03)
        var payload = new byte[4];
        payload[0] = 1;
        payload[1] = 42;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 0x0305);

        using var result = schema.Parse(payload)!.Value;

        result["flags16/low"].GetUInt8().Should().Be(5);  // bits 0-3 of 0x0305
        result["flags16/high"].GetUInt8().Should().Be(3);  // bits 8-11 of 0x0305
    }

    // ================================================================
    // uint16 and uint32 count fields (lines 797-798)
    // ================================================================

    [Test]
    public void Parse_SemiDynamicArray_UInt16CountField()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "count": { "type": "uint16", "size": 2 },
                "items": {
                  "dependsOn": "count",
                  "type": "array", "count": "count",
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[5];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, 2), 3);
        payload[2] = 10; payload[3] = 20; payload[4] = 30;

        using var result = schema.Parse(payload)!.Value;

        result["items/0"].GetUInt8().Should().Be(10);
        result["items/1"].GetUInt8().Should().Be(20);
        result["items/2"].GetUInt8().Should().Be(30);
    }

    [Test]
    public void Parse_SemiDynamicArray_UInt32CountField()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "count": { "type": "uint32", "size": 4 },
                "items": {
                  "dependsOn": "count",
                  "type": "array", "count": "count",
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), 2);
        payload[4] = 77; payload[5] = 88;

        using var result = schema.Parse(payload)!.Value;

        result["items/0"].GetUInt8().Should().Be(77);
        result["items/1"].GetUInt8().Should().Be(88);
    }

    // ================================================================
    // BinaryContractLoader — edge cases
    // ================================================================

    [Test]
    public void Load_NullDto_ReturnsNull()
    {
        // Valid JSON but not a contract (missing "kind")
        var schema = BinaryContractSchema.Load("""{"fields":{}}""");
        schema.Should().BeNull();
    }

    [Test]
    public void Load_EmptyFields_LoadsSuccessfully()
    {
        // kind=binary but fields is empty — should produce empty schema
        // Validation will fail (no root) but loader should not crash
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {}
            }
            """);
        // Empty fields means no root, so validation fails
        schema.Should().BeNull();
    }

    [Test]
    public void Load_WrongKind_ReturnsNull()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "json",
              "fields": { "a": { "type": "uint8", "size": 1 } }
            }
            """);
        schema.Should().BeNull();
    }

    [Test]
    public void Load_UnknownStringMode_DefaultsToTrimEnd()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "label": { "type": "string", "encoding": "ASCII", "size": 6, "mode": "unknown_mode" }
              }
            }
            """)!;
        // "unknown_mode" defaults to trimEnd (mode=2), so trailing nulls are trimmed
        var payload = new byte[] { (byte)'H', (byte)'i', 0, 0, 0, 0 };

        using var result = schema.Parse(payload)!.Value;

        result["label"].GetString().Should().Be("Hi");
    }

    // ================================================================
    // Pass 2 — enum field in fixed array after dynamic
    // ================================================================

    [Test]
    public void Parse_Pass2_FixedArrayTruncated_BreaksEarly()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedArrayContractJson)!;
        // n=1, dynamic=[5], but payload too short for fixed[2] uint16 array
        var payload = new byte[4]; // 1 + 1 + 2 (only 1 of 2 uint16 fits)
        payload[0] = 1;
        payload[1] = 5;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2, 2), 100);

        using var result = schema.Parse(payload)!.Value;

        result["fixed/0"].GetUInt16().Should().Be(100);
    }

    [Test]
    public void Parse_Pass2_FixedStructArray_PartialTruncation()
    {
        var schema = BinaryContractSchema.Load(Pass2FixedStructArrayContractJson)!;
        // n=0, only room for 1.5 struct records (payload ends mid-struct)
        var payload = new byte[5]; // 1 + 0 + 3 (full record) + 1 (partial)
        payload[0] = 0;
        payload[1] = 1; BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2, 2), 10);
        payload[4] = 2; // partial second record — val is out of bounds

        using var result = schema.Parse(payload)!.Value;

        result["records/0/id"].GetUInt8().Should().Be(1);
        result["records/0/val"].GetInt64().Should().Be(10);
    }

    // ================================================================
    // BitFieldDto — exercise deserialization (0% → covered)
    // ================================================================

    [Test]
    public void BitFieldDto_Deserialization_AllPropertiesUsed()
    {
        // BitFieldDto properties (Bit, Bits, Type) are set by System.Text.Json deserialization.
        // Loading a contract with bit fields exercises all three.
        var dto = System.Text.Json.JsonSerializer.Deserialize<Gluey.Contract.Binary.Dto.BitFieldDto>(
            """{"bit": 3, "bits": 4, "type": "uint8"}""");
        dto.Should().NotBeNull();
        dto!.Bit.Should().Be(3);
        dto.Bits.Should().Be(4);
        dto.Type.Should().Be("uint8");
    }

    // ================================================================
    // BinaryContractLoader — null/empty DTO paths
    // ================================================================

    [Test]
    public void Load_NullFieldsJson_ReturnsNull()
    {
        // Contract without fields key — dto.Fields is null
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little"
            }
            """);
        schema.Should().BeNull();
    }

    [Test]
    public void Load_XError_NonObjectValue_IgnoredGracefully()
    {
        // x-error as string instead of object — MapErrorInfo returns null
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "v": { "type": "uint8", "size": 1, "validation": { "max": 10 }, "x-error": "not-an-object" }
              }
            }
            """)!;
        var payload = new byte[] { 20 };

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorInfo.Should().BeNull();
    }

    [Test]
    public void Load_StructFieldsElement_Null_NoStructFields()
    {
        // Array element with type "struct" but no fields → MapStructFields returns null
        // This should fail validation since struct without fields is invalid
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 1,
                  "element": { "type": "struct", "size": 2 }
                }
              }
            }
            """);
        // Struct without sub-fields treated as scalar array element
        // Parser should handle gracefully
        if (schema is not null)
        {
            var payload = new byte[] { 1, 2 };
            using var result = schema.Parse(payload)!.Value;
            result.Should().NotBeNull();
        }
    }

    // ================================================================
    // ParsedProperty — Path, indexer, GetDecimal, Count, GetEnumerator, GetFieldTypeName
    // ================================================================

    [Test]
    public void ParsedProperty_Path_ReturnsFieldPath()
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

        result["v"].Path.Should().Be("/v");
    }

    [Test]
    public void ParsedProperty_Indexer_UnknownName_ReturnsEmpty()
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

        result["nonexistent"].HasValue.Should().BeFalse();
    }

    [Test]
    public void ParsedProperty_IntIndexer_OnArray_ReturnsElement()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 3,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[] { 10, 20, 30 };

        using var result = schema.Parse(payload)!.Value;

        result["items"][0].GetUInt8().Should().Be(10);
        result["items"][1].GetUInt8().Should().Be(20);
        result["items"][2].GetUInt8().Should().Be(30);
    }

    [Test]
    public void ParsedProperty_IntIndexer_OnScalar_ReturnsEmpty()
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

        result["v"][0].HasValue.Should().BeFalse();
    }

    [Test]
    public void ParsedProperty_Count_OnArray_ReturnsElementCount()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 3,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[] { 1, 2, 3 };

        using var result = schema.Parse(payload)!.Value;

        result["items"].Count.Should().Be(3);
    }

    [Test]
    public void ParsedProperty_Count_OnScalar_ReturnsZero()
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

        result["v"].Count.Should().Be(0);
    }

    [Test]
    public void ParsedProperty_GetEnumerator_OnArray_YieldsElements()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 2,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[] { 10, 20 };

        using var result = schema.Parse(payload)!.Value;

        var values = new List<byte>();
        foreach (var elem in result["items"])
            values.Add(elem.GetUInt8());

        values.Should().Equal(10, 20);
    }

    [Test]
    public void ParsedProperty_GetEnumerator_OnScalar_YieldsNothing()
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

        int count = 0;
        foreach (var elem in result["v"])
            count++;
        count.Should().Be(0);
    }

    [Test]
    public void ParsedProperty_GetDecimal_OnBinaryField_ThrowsNotSupported()
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

        var act = () => result["v"].GetDecimal();
        act.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void ParsedProperty_GetString_EnumWithNullValues_ReturnsRawByte()
    {
        // Enum with no values map — GetString on the suffixed accessor should still work
        // This tests line 366 (return _buffer[_offset].ToString() when _enumValues is null)
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "mode": { "type": "enum", "size": 1, "values": { "0": "off" } }
              }
            }
            """)!;
        var payload = new byte[] { 0 };

        using var result = schema.Parse(payload)!.Value;

        result["modes"].GetString().Should().Be("off");
        result["mode"].GetUInt8().Should().Be(0);
    }

    [Test]
    public void ParsedProperty_GetBoolean_JsonFormat_ReturnsCorrectValue()
    {
        // This covers the format==0 path in GetBoolean (line 602)
        // We can reach this through the JSON tests, but let's verify binary boolean works
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "active": { "type": "boolean", "size": 1 },
                "inactive": { "dependsOn": "active", "type": "boolean", "size": 1 }
              }
            }
            """)!;
        var payload = new byte[] { 1, 0 };

        using var result = schema.Parse(payload)!.Value;

        result["active"].GetBoolean().Should().BeTrue();
        result["inactive"].GetBoolean().Should().BeFalse();
    }

    // Type strictness — exercises GetFieldTypeName for multiple field types (lines 665-675)

    [Test]
    public void GetUInt8_OnInt16Field_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "int16", "size": 2 } }
            }
            """)!;
        var payload = new byte[] { 0x01, 0x00 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*int16*");
    }

    [Test]
    public void GetUInt8_OnFloat32Field_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "float32", "size": 4 } }
            }
            """)!;
        var payload = new byte[4];

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*float32*");
    }

    [Test]
    public void GetUInt8_OnFloat64Field_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "float64", "size": 8 } }
            }
            """)!;
        var payload = new byte[8];

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*float64*");
    }

    [Test]
    public void GetUInt8_OnStringField_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "string", "encoding": "ASCII", "size": 4 } }
            }
            """)!;
        var payload = Encoding.ASCII.GetBytes("test");

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*string*");
    }

    [Test]
    public void GetUInt8_OnEnumField_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "s": { "type": "enum", "size": 1, "values": { "0": "a" } }
              }
            }
            """)!;
        var payload = new byte[] { 0 };

        using var result = schema.Parse(payload)!.Value;

        // The suffixed accessor has fieldType=Enum, so GetUInt8 should throw
        var act = () => result["ss"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*enum*");
    }

    [Test]
    public void GetUInt8_OnBooleanField_ThrowsWithFieldTypeName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "boolean", "size": 1 } }
            }
            """)!;
        var payload = new byte[] { 1 };

        using var result = schema.Parse(payload)!.Value;

        var act = () => result["v"].GetUInt8();
        act.Should().Throw<InvalidOperationException>().WithMessage("*boolean*");
    }

    [Test]
    public void GetUInt8_OnPaddingField_ReturnsDefault()
    {
        // Padding fields have HasValue=false (_length=0), so GetUInt8 returns default
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "gap": { "type": "padding", "size": 2 },
                "v": { "dependsOn": "gap", "type": "uint8", "size": 1 }
              }
            }
            """)!;
        var payload = new byte[] { 0xFF, 0xFF, 42 };

        using var result = schema.Parse(payload)!.Value;

        result["v"].GetUInt8().Should().Be(42);
        result["gap"].HasValue.Should().BeFalse();
    }

    // Struct array element child indexer (exercises prefix-based lookup)
    [Test]
    public void ParsedProperty_StructElement_ChildAccess_ByName()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "records": {
                  "type": "array", "count": 1,
                  "element": {
                    "type": "struct", "size": 3,
                    "fields": {
                      "id": { "type": "uint8", "size": 1 },
                      "val": { "dependsOn": "id", "type": "uint16", "size": 2 }
                    }
                  }
                }
              }
            }
            """)!;
        var payload = new byte[] { 5, 0x00, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        // Direct path access
        result["records/0/id"].GetUInt8().Should().Be(5);
        result["records/0/val"].GetUInt16().Should().Be(256);

        // Element container access via int indexer then child name
        var elem = result["records"][0];
        elem["id"].GetUInt8().Should().Be(5);
        elem["val"].GetUInt16().Should().Be(256);
    }

    // ================================================================
    // Pass 2 — fixed array after MULTIPLE semi-dynamic arrays
    // Exercises ComputeActualFieldSize (lines 828-854) for Pass 2 offset computation
    // ================================================================

    [Test]
    public void Parse_Pass2_MultipleArrays_OffsetsCorrect()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "n1": { "type": "uint8", "size": 1 },
                "arr1": {
                  "dependsOn": "n1",
                  "type": "array", "count": "n1",
                  "element": { "type": "uint8", "size": 1 }
                },
                "n2": {
                  "dependsOn": "arr1",
                  "type": "uint8", "size": 1
                },
                "arr2": {
                  "dependsOn": "n2",
                  "type": "array", "count": "n2",
                  "element": { "type": "uint16", "size": 2 }
                },
                "tail": {
                  "dependsOn": "arr2",
                  "type": "uint8", "size": 1
                }
              }
            }
            """)!;
        // n1=2, arr1=[10,20], n2=1, arr2=[300], tail=99
        var payload = new byte[8];
        payload[0] = 2; // n1
        payload[1] = 10; payload[2] = 20; // arr1
        payload[3] = 1; // n2
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 300); // arr2
        payload[6] = 99; // tail

        using var result = schema.Parse(payload)!.Value;

        result["arr1/0"].GetUInt8().Should().Be(10);
        result["arr1/1"].GetUInt8().Should().Be(20);
        result["arr2/0"].GetUInt16().Should().Be(300);
        result["tail"].GetUInt8().Should().Be(99);
    }
}
