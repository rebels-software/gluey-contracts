// Copyright 2025 Rebels Software sp. z o.o.
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
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class SchemaWalkerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static JsonContractSchema LoadSchema(string json, bool assertFormat = false)
    {
        var options = assertFormat ? new SchemaOptions { AssertFormat = true } : null;
        return JsonContractSchema.Load(json, options: options)!;
    }

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Valid parsing ────────────────────────────────────────────────────

    [Test]
    public void TryParse_ValidObject_ReturnsTrue()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        var data = Utf8("""{"name":"Alice"}""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.IsValid.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_ValidObject_PopulatesOffsetTable()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}}}""");
        var data = Utf8("""{"name":"Bob","age":30}""");

        schema.TryParse(data, out var result);

        // OffsetTable keyed by RFC 6901 path (SchemaIndexer uses child.Path)
        result["/name"].HasValue.Should().BeTrue();
        result["/name"].GetString().Should().Be("Bob");
        result["/age"].HasValue.Should().BeTrue();
        result["/age"].GetInt32().Should().Be(30);
        result.Dispose();
    }

    // ── Type mismatch ────────────────────────────────────────────────────

    [Test]
    public void TryParse_TypeMismatch_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"string"}""");
        var data = Utf8("42");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Dispose();
    }

    [Test]
    public void Parse_TypeMismatch_ReturnsResultWithErrors()
    {
        var schema = LoadSchema("""{"type":"string"}""");
        var data = Utf8("42");

        var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors.Count.Should().BeGreaterThan(0);
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.TypeMismatch);
        result.Value.Dispose();
    }

    // ── Missing required ─────────────────────────────────────────────────

    [Test]
    public void TryParse_MissingRequired_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        var data = Utf8("{}");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.RequiredMissing);
        result.Dispose();
    }

    // ── AdditionalProperties false ───────────────────────────────────────

    [Test]
    public void TryParse_AdditionalPropertiesFalse_RejectsExtra()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":false}""");
        var data = Utf8("""{"name":"Alice","extra":"value"}""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.AdditionalPropertyNotAllowed);
        result.Dispose();
    }

    // ── Numeric constraints ──────────────────────────────────────────────

    [Test]
    public void TryParse_MinimumViolation_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"number","minimum":10}""");
        var data = Utf8("5");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
        result.Dispose();
    }

    // ── String constraints ───────────────────────────────────────────────

    [Test]
    public void TryParse_MinLengthViolation_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"string","minLength":5}""");
        var data = Utf8("\"ab\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinLengthExceeded);
        result.Dispose();
    }

    // ── Array constraints ────────────────────────────────────────────────

    [Test]
    public void TryParse_MinItemsViolation_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"integer"},"minItems":3}""");
        var data = Utf8("[1]");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.MinItemsExceeded);
        result.Dispose();
    }

    // ── Enum/Const ───────────────────────────────────────────────────────

    [Test]
    public void TryParse_EnumViolation_ReturnsFalse()
    {
        var schema = LoadSchema("""{"enum":["red","green","blue"]}""");
        var data = Utf8("\"yellow\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.EnumMismatch);
        result.Dispose();
    }

    [Test]
    public void TryParse_ConstValid_ReturnsTrue()
    {
        var schema = LoadSchema("""{"const":"hello"}""");
        var data = Utf8("\"hello\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    // ── Composition: allOf ───────────────────────────────────────────────

    [Test]
    public void TryParse_AllOfValid_ReturnsTrue()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"number"},{"minimum":0}]}""");
        var data = Utf8("5");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_AllOfInvalid_ReturnsFalse()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"number"},{"minimum":10}]}""");
        var data = Utf8("5");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Dispose();
    }

    // ── Malformed JSON ───────────────────────────────────────────────────

    [Test]
    public void Parse_MalformedJson_ReturnsNull()
    {
        var schema = LoadSchema("""{"type":"object"}""");
        var data = Utf8("{invalid json");

        var result = schema.Parse(data);

        result.Should().BeNull();
    }

    // ── Boolean schema ───────────────────────────────────────────────────

    [Test]
    public void TryParse_BooleanSchemaFalse_Rejects()
    {
        // Boolean schema false: schema that rejects everything
        // We need a schema that has a subschema = false. Use "not": true (equivalent to false for the value).
        // Actually, let's use additionalProperties:false and pass an extra prop.
        // For a true boolean schema false test, we need a way to create it.
        // Using allOf with a false subschema (not keyword validates the opposite)
        var schema = LoadSchema("""{"not":{}}""");
        var data = Utf8("42");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.NotInvalid);
        result.Dispose();
    }

    // ── $ref transparent follow ──────────────────────────────────────────

    [Test]
    public void TryParse_RefFollow_ValidatesAgainstTarget()
    {
        var schema = LoadSchema("""{"$ref":"#/$defs/str","$defs":{"str":{"type":"string"}}}""");
        var data = Utf8("\"hello\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_RefFollow_RejectsInvalid()
    {
        var schema = LoadSchema("""{"$ref":"#/$defs/str","$defs":{"str":{"type":"string"}}}""");
        var data = Utf8("42");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Dispose();
    }

    // ── Format assertion ─────────────────────────────────────────────────

    [Test]
    public void TryParse_FormatAsserted_RejectsInvalidEmail()
    {
        var schema = LoadSchema("""{"type":"string","format":"email"}""", assertFormat: true);
        var data = Utf8("\"not-an-email\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Errors[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
        result.Dispose();
    }

    [Test]
    public void TryParse_FormatNotAsserted_AcceptsInvalidEmail()
    {
        var schema = LoadSchema("""{"type":"string","format":"email"}""", assertFormat: false);
        var data = Utf8("\"not-an-email\"");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    // ── Multiple errors collected ────────────────────────────────────────

    [Test]
    public void Parse_MultipleErrors_CollectsAll()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"a":{"type":"string"},"b":{"type":"string"}},"required":["a","b"]}""");
        var data = Utf8("{}");

        var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.Errors.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Value.Dispose();
    }

    // ── ReadOnlySpan overload validates without OffsetTable ───────────────

    [Test]
    public void TryParse_SpanOverload_ValidatesButNoOffsetTable()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        ReadOnlySpan<byte> data = Utf8("""{"name":"Alice"}""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        // Span overload does not populate OffsetTable -- indexers return Empty
        result["/name"].HasValue.Should().BeFalse();
        result.Dispose();
    }

    // ── Nested object validation ─────────────────────────────────────────

    [Test]
    public void TryParse_NestedObject_ValidatesRecursively()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"address":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}}}""");
        var data = Utf8("""{"address":{"city":"NYC"}}""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_NestedObject_ValidationFailure()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"address":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}}}""");
        var data = Utf8("""{"address":{}}""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Dispose();
    }

    // ── Array with items validation ──────────────────────────────────────

    [Test]
    public void TryParse_ArrayItems_ValidatesEachElement()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"string"}}""");
        var data = Utf8("""["a","b","c"]""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_ArrayItems_RejectsInvalidElement()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"string"}}""");
        var data = Utf8("""["a",42,"c"]""");

        bool success = schema.TryParse(data, out var result);

        success.Should().BeFalse();
        result.Dispose();
    }

    // ── InvalidJson error code exists ────────────────────────────────────

    [Test]
    public void InvalidJson_ErrorCode_Exists()
    {
        ValidationErrorCode code = ValidationErrorCode.InvalidJson;
        code.Should().NotBe(ValidationErrorCode.None);
        ValidationErrorMessages.Get(code).Should().NotBeEmpty();
    }
}
