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

[TestFixture]
internal sealed class ValidationTests
{
    // ================================================================
    // Contract JSON definitions
    // ================================================================

    // Contract 1: Numeric min/max (temperature int16 + humidity uint8 = 3 bytes)
    private const string NumericMinMaxContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": { "type": "int16", "size": 2, "validation": { "min": -40, "max": 85 } },
            "humidity":    { "dependsOn": "temperature", "type": "uint8", "size": 1 }
          }
        }
        """;

    // Contract 2: String pattern validation (deviceId string ASCII 8 bytes)
    private const string StringPatternContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "deviceId": { "type": "string", "encoding": "ASCII", "size": 8, "validation": { "pattern": "^DEV-[0-9]{4}$" } }
          }
        }
        """;

    // Contract 3: String minLength/maxLength (label string ASCII 10 bytes)
    private const string StringLengthContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "label": { "type": "string", "encoding": "ASCII", "size": 10, "validation": { "minLength": 3, "maxLength": 8 } }
          }
        }
        """;

    // Contract 4: Payload too short (uint16 + uint16 + uint8 = 5 bytes)
    private const string PayloadSizeContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "fieldA": { "type": "uint16", "size": 2 },
            "fieldB": { "dependsOn": "fieldA", "type": "uint16", "size": 2 },
            "fieldC": { "dependsOn": "fieldB", "type": "uint8",  "size": 1 }
          }
        }
        """;

    // Contract 5: Multiple errors (temperature + humidity + pressure = 5 bytes)
    private const string MultiErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": { "type": "int16",  "size": 2, "validation": { "min": -40, "max": 85 } },
            "humidity":    { "dependsOn": "temperature", "type": "uint8",  "size": 1, "validation": { "min": 0, "max": 100 } },
            "pressure":    { "dependsOn": "humidity",    "type": "uint16", "size": 2, "validation": { "min": 300, "max": 1100 } }
          }
        }
        """;

    // Contract 6: Mixed numeric + string errors (temperature + deviceId = 10 bytes)
    private const string MixedErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": { "type": "int16", "size": 2, "validation": { "max": 85 } },
            "deviceId":    { "dependsOn": "temperature", "type": "string", "encoding": "ASCII", "size": 8, "validation": { "pattern": "^DEV-" } }
          }
        }
        """;

    // Contract 7: Unsigned field with min/max (sensorId uint32 = 4 bytes)
    private const string UnsignedMinMaxContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "sensorId": { "type": "uint32", "size": 4, "validation": { "min": 1000, "max": 9999 } }
          }
        }
        """;

    // Contract 8: Float field with min/max (voltage float32 = 4 bytes)
    private const string FloatMinMaxContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "voltage": { "type": "float32", "size": 4, "validation": { "min": 0.0, "max": 5.0 } }
          }
        }
        """;

    // ================================================================
    // VALD-01: Numeric min/max validation
    // ================================================================

    [Test]
    public void Parse_NumericBelowMin_CollectsMinimumExceededError()
    {
        var schema = BinaryContractSchema.Load(NumericMinMaxContractJson)!;
        var payload = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), -50); // below min -40
        payload[2] = 50; // humidity

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Be("/temperature");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
        // D-02: Invalid value still accessible
        result["temperature"].GetInt64().Should().Be(-50);
    }

    [Test]
    public void Parse_NumericAboveMax_CollectsMaximumExceededError()
    {
        var schema = BinaryContractSchema.Load(NumericMinMaxContractJson)!;
        var payload = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 100); // above max 85
        payload[2] = 50;

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Be("/temperature");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
        // D-02: Invalid value still accessible
        result["temperature"].GetInt64().Should().Be(100);
    }

    [Test]
    public void Parse_NumericWithinRange_NoErrors()
    {
        var schema = BinaryContractSchema.Load(NumericMinMaxContractJson)!;
        var payload = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 25); // within -40..85
        payload[2] = 50;

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }

    [Test]
    public void Parse_NumericAtBoundary_NoErrors()
    {
        var schema = BinaryContractSchema.Load(NumericMinMaxContractJson)!;
        var payload = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 85); // exactly at max (inclusive)
        payload[2] = 50;

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }

    // ================================================================
    // VALD-02: String pattern validation
    // ================================================================

    [Test]
    public void Parse_StringMatchesPattern_NoErrors()
    {
        var schema = BinaryContractSchema.Load(StringPatternContractJson)!;
        var payload = Encoding.ASCII.GetBytes("DEV-1234"); // matches ^DEV-[0-9]{4}$

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }

    [Test]
    public void Parse_StringFailsPattern_CollectsPatternMismatchError()
    {
        var schema = BinaryContractSchema.Load(StringPatternContractJson)!;
        var payload = Encoding.ASCII.GetBytes("INVALID!"); // does not match pattern

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
        result.Errors[0].Path.Should().Be("/deviceId");
        // D-02: Invalid value still accessible
        result["deviceId"].GetString().Should().Be("INVALID!");
    }

    // ================================================================
    // VALD-03: String minLength/maxLength validation
    // ================================================================

    [Test]
    public void Parse_StringTooShort_CollectsMinLengthExceededError()
    {
        var schema = BinaryContractSchema.Load(StringLengthContractJson)!;
        // "AB" followed by 8 null bytes (trimmed length 2 < minLength 3)
        var payload = new byte[10];
        payload[0] = (byte)'A';
        payload[1] = (byte)'B';

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinLengthExceeded);
        result.Errors[0].Path.Should().Be("/label");
        // D-02: Invalid value still accessible
        result["label"].GetString().Should().Be("AB");
    }

    [Test]
    public void Parse_StringTooLong_CollectsMaxLengthExceededError()
    {
        var schema = BinaryContractSchema.Load(StringLengthContractJson)!;
        // "ABCDEFGHI\0" = 9 meaningful chars + 1 null (trimmed length 9 > maxLength 8)
        var payload = new byte[10];
        Encoding.ASCII.GetBytes("ABCDEFGHI", payload.AsSpan(0, 9));
        payload[9] = 0x00;

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaxLengthExceeded);
        result.Errors[0].Path.Should().Be("/label");
        // D-02: Invalid value still accessible
        result["label"].GetString().Should().Be("ABCDEFGHI");
    }

    [Test]
    public void Parse_StringWithinLengthRange_NoErrors()
    {
        var schema = BinaryContractSchema.Load(StringLengthContractJson)!;
        // "HELLO" + 5 nulls (trimmed length 5, within 3-8 range)
        var payload = new byte[10];
        Encoding.ASCII.GetBytes("HELLO", payload.AsSpan(0, 5));

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
        result["label"].GetString().Should().Be("HELLO");
    }

    // ================================================================
    // VALD-04: Payload too short returns null
    // ================================================================

    [Test]
    public void Parse_PayloadTooShort_ReturnsNull()
    {
        var schema = BinaryContractSchema.Load(PayloadSizeContractJson)!;
        // Contract needs 5 bytes, payload only 3
        var payload = new byte[] { 0x01, 0x02, 0x03 };

        var result = schema.Parse(payload);

        result.Should().BeNull();
    }

    [Test]
    public void Parse_PayloadExactSize_ReturnsResult()
    {
        var schema = BinaryContractSchema.Load(PayloadSizeContractJson)!;
        // Exactly 5 bytes
        var payload = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 };

        var result = schema.Parse(payload);

        result.Should().NotBeNull();
        result!.Value.Dispose();
    }

    // ================================================================
    // VALD-05: Multiple errors collected
    // ================================================================

    [Test]
    public void Parse_MultipleFieldsInvalid_CollectsAllErrors()
    {
        var schema = BinaryContractSchema.Load(MultiErrorContractJson)!;
        var payload = new byte[5];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 100);  // temperature above max 85
        payload[2] = 150;                                                     // humidity above max 100
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3, 2), 200); // pressure below min 300

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(3);

        // Verify each error has correct path and code
        var errors = new List<(string Path, ValidationErrorCode Code)>();
        for (int i = 0; i < result.Errors.Count; i++)
            errors.Add((result.Errors[i].Path, result.Errors[i].Code));

        errors.Should().Contain(e => e.Path == "/temperature" && e.Code == ValidationErrorCode.MaximumExceeded);
        errors.Should().Contain(e => e.Path == "/humidity" && e.Code == ValidationErrorCode.MaximumExceeded);
        errors.Should().Contain(e => e.Path == "/pressure" && e.Code == ValidationErrorCode.MinimumExceeded);

        // D-02: All three values still accessible despite errors
        result["temperature"].GetInt64().Should().Be(100);
        result["humidity"].GetUInt8().Should().Be(150);
        result["pressure"].GetUInt16().Should().Be(200);
    }

    [Test]
    public void Parse_MixedNumericAndStringErrors_CollectsAll()
    {
        var schema = BinaryContractSchema.Load(MixedErrorContractJson)!;
        var payload = new byte[10];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 100); // above max 85
        Encoding.ASCII.GetBytes("INVALID!", payload.AsSpan(2, 8));          // fails ^DEV- pattern

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(2);

        var errors = new List<(string Path, ValidationErrorCode Code)>();
        for (int i = 0; i < result.Errors.Count; i++)
            errors.Add((result.Errors[i].Path, result.Errors[i].Code));

        errors.Should().Contain(e => e.Path == "/temperature" && e.Code == ValidationErrorCode.MaximumExceeded);
        errors.Should().Contain(e => e.Path == "/deviceId" && e.Code == ValidationErrorCode.PatternMismatch);
    }

    // ================================================================
    // Additional edge cases
    // ================================================================

    [Test]
    public void Parse_UnsignedFieldWithMinMax_Validates()
    {
        var schema = BinaryContractSchema.Load(UnsignedMinMaxContractJson)!;
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 500); // below min 1000

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
        result.Errors[0].Path.Should().Be("/sensorId");
        // D-02: Value still accessible
        result["sensorId"].GetUInt32().Should().Be(500u);
    }

    [Test]
    public void Parse_FloatFieldWithMinMax_Validates()
    {
        var schema = BinaryContractSchema.Load(FloatMinMaxContractJson)!;
        var payload = BitConverter.GetBytes(6.5f); // above max 5.0

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
        result.Errors[0].Path.Should().Be("/voltage");
        // D-02: Value still accessible
        result["voltage"].GetDouble().Should().BeApproximately(6.5, 0.001);
    }

    [Test]
    public void Parse_NumericAtMinBoundary_NoErrors()
    {
        var schema = BinaryContractSchema.Load(NumericMinMaxContractJson)!;
        var payload = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), -40); // exactly at min (inclusive)
        payload[2] = 50;

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }
}
