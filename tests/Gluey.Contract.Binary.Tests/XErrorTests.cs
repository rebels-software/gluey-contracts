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
internal sealed class XErrorTests
{
    // ================================================================
    // x-error on numeric field (uint8 with min/max)
    // ================================================================

    private const string NumericXErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "level": {
              "type": "uint8", "size": 1,
              "validation": { "min": 0, "max": 100 },
              "x-error": {
                "code": "INVALID_LEVEL",
                "title": "Invalid battery level",
                "detail": "Battery level must be between 0 and 100",
                "type": "https://api.example.com/errors/invalid-level"
              }
            }
          }
        }
        """;

    [Test]
    public void Parse_NumericXError_DetailOverridesMessage()
    {
        var schema = BinaryContractSchema.Load(NumericXErrorContractJson)!;
        var payload = new byte[] { 150 }; // above max 100

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(1);
        result.Errors[0].Path.Should().Be("/level");
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
        result.Errors[0].Message.Should().Be("Battery level must be between 0 and 100");
        result.Errors[0].ErrorInfo.Should().NotBeNull();
        result.Errors[0].ErrorInfo!.Value.Code.Should().Be("INVALID_LEVEL");
        result.Errors[0].ErrorInfo!.Value.Title.Should().Be("Invalid battery level");
        result.Errors[0].ErrorInfo!.Value.Detail.Should().Be("Battery level must be between 0 and 100");
        result.Errors[0].ErrorInfo!.Value.Type.Should().Be("https://api.example.com/errors/invalid-level");
    }

    [Test]
    public void Parse_NumericXError_ValidValue_NoErrors()
    {
        var schema = BinaryContractSchema.Load(NumericXErrorContractJson)!;
        var payload = new byte[] { 50 };

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }

    // ================================================================
    // x-error on string field (pattern + minLength)
    // ================================================================

    private const string StringXErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "badgeId": {
              "type": "string", "size": 8, "encoding": "ascii",
              "validation": { "pattern": "^[A-Z0-9]+$", "minLength": 3 },
              "x-error": {
                "code": "INVALID_BADGE",
                "detail": "Badge ID must be 3+ uppercase alphanumeric characters"
              }
            }
          }
        }
        """;

    [Test]
    public void Parse_StringXError_PatternMismatch_DetailOverridesMessage()
    {
        var schema = BinaryContractSchema.Load(StringXErrorContractJson)!;
        var payload = Encoding.ASCII.GetBytes("abc!!!!!");

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
        result.Errors[0].Message.Should().Be("Badge ID must be 3+ uppercase alphanumeric characters");
        result.Errors[0].ErrorInfo!.Value.Code.Should().Be("INVALID_BADGE");
    }

    [Test]
    public void Parse_StringXError_MinLengthExceeded_DetailOverridesMessage()
    {
        var schema = BinaryContractSchema.Load(StringXErrorContractJson)!;
        // "AB" + 6 null bytes → trimmed length 2 < minLength 3
        var payload = new byte[8];
        payload[0] = (byte)'A';
        payload[1] = (byte)'B';

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinLengthExceeded);
        result.Errors[0].Message.Should().Be("Badge ID must be 3+ uppercase alphanumeric characters");
        result.Errors[0].ErrorInfo!.Value.Code.Should().Be("INVALID_BADGE");
    }

    // ================================================================
    // x-error partial fields — only code, no detail
    // ================================================================

    private const string PartialXErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": {
              "type": "int16", "size": 2,
              "validation": { "min": -40, "max": 85 },
              "x-error": { "code": "TEMP_OUT_OF_RANGE" }
            }
          }
        }
        """;

    [Test]
    public void Parse_PartialXError_DefaultMessagePreservedWhenNoDetail()
    {
        var schema = BinaryContractSchema.Load(PartialXErrorContractJson)!;
        var payload = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(payload, 100); // above max 85

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
        // No detail → falls back to default message
        result.Errors[0].Message.Should().Be(ValidationErrorMessages.Get(ValidationErrorCode.MaximumExceeded));
        result.Errors[0].ErrorInfo.Should().NotBeNull();
        result.Errors[0].ErrorInfo!.Value.Code.Should().Be("TEMP_OUT_OF_RANGE");
        result.Errors[0].ErrorInfo!.Value.Detail.Should().BeNull();
    }

    // ================================================================
    // No x-error — ErrorInfo is null (existing behavior preserved)
    // ================================================================

    [Test]
    public void Parse_NoXError_ErrorInfoIsNull()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "level": { "type": "uint8", "size": 1, "validation": { "max": 100 } }
              }
            }
            """)!;
        var payload = new byte[] { 200 };

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorInfo.Should().BeNull();
    }

    // ================================================================
    // x-error on multiple fields — each gets its own error info
    // ================================================================

    private const string MultiFieldXErrorContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "temperature": {
              "type": "int16", "size": 2,
              "validation": { "max": 85 },
              "x-error": { "code": "TEMP_ERROR", "detail": "Temperature too high" }
            },
            "deviceId": {
              "dependsOn": "temperature",
              "type": "string", "size": 8, "encoding": "ascii",
              "validation": { "pattern": "^DEV-" },
              "x-error": { "code": "DEVICE_ERROR", "detail": "Invalid device ID format" }
            }
          }
        }
        """;

    [Test]
    public void Parse_MultipleFieldsWithXError_EachGetsOwnErrorInfo()
    {
        var schema = BinaryContractSchema.Load(MultiFieldXErrorContractJson)!;
        var payload = new byte[10];
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0, 2), 100); // above max 85
        Encoding.ASCII.GetBytes("INVALID!", payload.AsSpan(2, 8));          // fails ^DEV-

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().Be(2);

        var tempError = result.Errors[0].Path == "/temperature" ? result.Errors[0] : result.Errors[1];
        var devError = result.Errors[0].Path == "/deviceId" ? result.Errors[0] : result.Errors[1];

        tempError.Message.Should().Be("Temperature too high");
        tempError.ErrorInfo!.Value.Code.Should().Be("TEMP_ERROR");

        devError.Message.Should().Be("Invalid device ID format");
        devError.ErrorInfo!.Value.Code.Should().Be("DEVICE_ERROR");
    }
}
