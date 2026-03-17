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
    public void Parse_ValidObject_ReturnsValid()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        var data = Utf8("""{"name":"Alice"}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_ValidObject_PopulatesOffsetTable()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}}}""");
        var data = Utf8("""{"name":"Bob","age":30}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        // OffsetTable keyed by RFC 6901 path (SchemaIndexer uses child.Path)
        result!.Value["/name"].HasValue.Should().BeTrue();
        result.Value["/name"].GetString().Should().Be("Bob");
        result.Value["/age"].HasValue.Should().BeTrue();
        result.Value["/age"].GetInt32().Should().Be(30);
    }

    // ── Type mismatch ────────────────────────────────────────────────────

    [Test]
    public void Parse_TypeMismatch_ReturnsFalse()
    {
        var schema = LoadSchema("""{"type":"string"}""");
        var data = Utf8("42");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
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
    public void Parse_MissingRequired_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        var data = Utf8("{}");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.RequiredMissing);
    }

    // ── AdditionalProperties false ───────────────────────────────────────

    [Test]
    public void Parse_AdditionalPropertiesFalse_RejectsExtra()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":false}""");
        var data = Utf8("""{"name":"Alice","extra":"value"}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.AdditionalPropertyNotAllowed);
    }

    // ── Numeric constraints ──────────────────────────────────────────────

    [Test]
    public void Parse_MinimumViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"number","minimum":10}""");
        var data = Utf8("5");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
    }

    // ── String constraints ───────────────────────────────────────────────

    [Test]
    public void Parse_MinLengthViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"string","minLength":5}""");
        var data = Utf8("\"ab\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MinLengthExceeded);
    }

    // ── Array constraints ────────────────────────────────────────────────

    [Test]
    public void Parse_MinItemsViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"integer"},"minItems":3}""");
        var data = Utf8("[1]");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MinItemsExceeded);
    }

    // ── Enum/Const ───────────────────────────────────────────────────────

    [Test]
    public void Parse_EnumViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"enum":["red","green","blue"]}""");
        var data = Utf8("\"yellow\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.EnumMismatch);
    }

    [Test]
    public void Parse_ConstValid_ReturnsValid()
    {
        var schema = LoadSchema("""{"const":"hello"}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── Composition: allOf ───────────────────────────────────────────────

    [Test]
    public void Parse_AllOfValid_ReturnsValid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"number"},{"minimum":0}]}""");
        var data = Utf8("5");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_AllOfInvalid_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"number"},{"minimum":10}]}""");
        var data = Utf8("5");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
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
    public void Parse_BooleanSchemaFalse_Rejects()
    {
        // Boolean schema false: schema that rejects everything
        // We need a schema that has a subschema = false. Use "not": true (equivalent to false for the value).
        // Actually, let's use additionalProperties:false and pass an extra prop.
        // For a true boolean schema false test, we need a way to create it.
        // Using allOf with a false subschema (not keyword validates the opposite)
        var schema = LoadSchema("""{"not":{}}""");
        var data = Utf8("42");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.NotInvalid);
    }

    // ── $ref transparent follow ──────────────────────────────────────────

    [Test]
    public void Parse_RefFollow_ValidatesAgainstTarget()
    {
        var schema = LoadSchema("""{"$ref":"#/$defs/str","$defs":{"str":{"type":"string"}}}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_RefFollow_RejectsInvalid()
    {
        var schema = LoadSchema("""{"$ref":"#/$defs/str","$defs":{"str":{"type":"string"}}}""");
        var data = Utf8("42");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── Format assertion ─────────────────────────────────────────────────

    [Test]
    public void Parse_FormatAsserted_RejectsInvalidEmail()
    {
        var schema = LoadSchema("""{"type":"string","format":"email"}""", assertFormat: true);
        var data = Utf8("\"not-an-email\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.FormatInvalid);
    }

    [Test]
    public void Parse_FormatNotAsserted_AcceptsInvalidEmail()
    {
        var schema = LoadSchema("""{"type":"string","format":"email"}""", assertFormat: false);
        var data = Utf8("\"not-an-email\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
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
    public void Parse_SpanOverload_ValidatesButNoOffsetTable()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");
        ReadOnlySpan<byte> data = Utf8("""{"name":"Alice"}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        // Span overload does not populate OffsetTable -- indexers return Empty
        result.Value["/name"].HasValue.Should().BeFalse();
    }

    // ── Nested object validation ─────────────────────────────────────────

    [Test]
    public void Parse_NestedObject_ValidatesRecursively()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"address":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}}}""");
        var data = Utf8("""{"address":{"city":"NYC"}}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_NestedObject_ValidationFailure()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"address":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}}}""");
        var data = Utf8("""{"address":{}}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── Array with items validation ──────────────────────────────────────

    [Test]
    public void Parse_ArrayItems_ValidatesEachElement()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"string"}}""");
        var data = Utf8("""["a","b","c"]""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_ArrayItems_RejectsInvalidElement()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"string"}}""");
        var data = Utf8("""["a",42,"c"]""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── InvalidJson error code exists ────────────────────────────────────

    [Test]
    public void InvalidJson_ErrorCode_Exists()
    {
        ValidationErrorCode code = ValidationErrorCode.InvalidJson;
        code.Should().NotBe(ValidationErrorCode.None);
        ValidationErrorMessages.Get(code).Should().NotBeEmpty();
    }

    // ── Const violation ──────────────────────────────────────────────────

    [Test]
    public void Parse_ConstViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"const":"hello"}""");
        var data = Utf8("\"world\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.ConstMismatch);
    }

    // ── Numeric constraints: maximum, exclusiveMin/Max, multipleOf ──────

    [Test]
    public void Parse_MaximumViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"number","maximum":10}""");
        var data = Utf8("15");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
    }

    [Test]
    public void Parse_ExclusiveMinimumViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"number","exclusiveMinimum":10}""");
        var data = Utf8("10");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.ExclusiveMinimumExceeded);
    }

    [Test]
    public void Parse_ExclusiveMaximumViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"number","exclusiveMaximum":10}""");
        var data = Utf8("10");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.ExclusiveMaximumExceeded);
    }

    [Test]
    public void Parse_MultipleOfViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"number","multipleOf":3}""");
        var data = Utf8("7");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MultipleOfInvalid);
    }

    // ── String constraints: maxLength, pattern ──────────────────────────

    [Test]
    public void Parse_MaxLengthViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"string","maxLength":3}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MaxLengthExceeded);
    }

    [Test]
    public void Parse_PatternViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"string","pattern":"^[a-z]+$"}""");
        var data = Utf8("\"ABC123\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
    }

    // ── Array constraints: maxItems ─────────────────────────────────────

    [Test]
    public void Parse_MaxItemsViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"array","items":{"type":"integer"},"maxItems":2}""");
        var data = Utf8("[1,2,3]");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MaxItemsExceeded);
    }

    // ── Composition: anyOf, oneOf, not ──────────────────────────────────

    [Test]
    public void Parse_AnyOfValid_ReturnsValid()
    {
        var schema = LoadSchema("""{"anyOf":[{"type":"string"},{"type":"number"}]}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_AnyOfInvalid_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"anyOf":[{"type":"string"},{"type":"number"}]}""");
        var data = Utf8("true");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.AnyOfInvalid);
    }

    [Test]
    public void Parse_OneOfValid_ReturnsValid()
    {
        var schema = LoadSchema("""{"oneOf":[{"type":"string"},{"type":"number"}]}""");
        var data = Utf8("42");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_OneOfMultipleMatch_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"oneOf":[{"type":"number"},{"minimum":0}]}""");
        var data = Utf8("5");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.OneOfInvalid);
    }

    [Test]
    public void Parse_NotValid_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"not":{"type":"string"}}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.NotInvalid);
    }

    [Test]
    public void Parse_NotInvalid_ReturnsValid()
    {
        var schema = LoadSchema("""{"not":{"type":"string"}}""");
        var data = Utf8("42");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── Conditionals: if/then/else ──────────────────────────────────────

    [Test]
    public void Parse_IfThen_ConditionTrue_AppliesThen()
    {
        var schema = LoadSchema("""{"if":{"type":"number"},"then":{"minimum":10}}""");
        var data = Utf8("5");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_IfThen_ConditionTrue_PassesThen()
    {
        var schema = LoadSchema("""{"if":{"type":"number"},"then":{"minimum":10}}""");
        var data = Utf8("15");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_IfElse_ConditionFalse_AppliesElse()
    {
        var schema = LoadSchema("""{"if":{"type":"number"},"else":{"type":"string"}}""");
        var data = Utf8("true");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_IfElse_ConditionFalse_PassesElse()
    {
        var schema = LoadSchema("""{"if":{"type":"number"},"else":{"type":"string"}}""");
        var data = Utf8("\"hello\"");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── Composition at object level ─────────────────────────────────────

    [Test]
    public void Parse_AllOfObject_Valid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"object","required":["a"]},{"type":"object","required":["b"]}]}""");
        var data = Utf8("""{"a":1,"b":2}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_AllOfObject_Invalid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"object","required":["a"]},{"type":"object","required":["b"]}]}""");
        var data = Utf8("""{"a":1}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── Conditionals at object level ────────────────────────────────────

    [Test]
    public void Parse_IfThenObject_ConditionTrue_AppliesThen()
    {
        var schema = LoadSchema("""
        {
            "type":"object",
            "properties":{"kind":{"type":"string"},"value":{}},
            "if":{"properties":{"kind":{"const":"number"}},"required":["kind"]},
            "then":{"properties":{"value":{"type":"number"}},"required":["value"]}
        }
        """);
        var data = Utf8("""{"kind":"number"}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_IfThenObject_ConditionTrue_PassesThen()
    {
        var schema = LoadSchema("""
        {
            "type":"object",
            "properties":{"kind":{"type":"string"},"value":{}},
            "if":{"properties":{"kind":{"const":"number"}},"required":["kind"]},
            "then":{"properties":{"value":{"type":"number"}},"required":["value"]}
        }
        """);
        var data = Utf8("""{"kind":"number","value":42}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── Composition at array level ──────────────────────────────────────

    [Test]
    public void Parse_AllOfArray_Valid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"array","minItems":1},{"type":"array","maxItems":3}]}""");
        var data = Utf8("[1,2]");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_AllOfArray_Invalid()
    {
        var schema = LoadSchema("""{"allOf":[{"type":"array","minItems":1},{"type":"array","maxItems":2}]}""");
        var data = Utf8("[1,2,3]");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── AdditionalProperties as schema ──────────────────────────────────

    [Test]
    public void Parse_AdditionalPropertiesSchema_ValidatesExtra()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":{"type":"number"}}""");
        var data = Utf8("""{"name":"Alice","extra":"notANumber"}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_AdditionalPropertiesSchema_AcceptsValidExtra()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":{"type":"number"}}""");
        var data = Utf8("""{"name":"Alice","extra":42}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── DependentRequired ───────────────────────────────────────────────

    [Test]
    public void Parse_DependentRequired_Violation()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"a":{},"b":{}},"dependentRequired":{"a":["b"]}}""");
        var data = Utf8("""{"a":1}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_DependentRequired_Satisfied()
    {
        var schema = LoadSchema("""{"type":"object","properties":{"a":{},"b":{}},"dependentRequired":{"a":["b"]}}""");
        var data = Utf8("""{"a":1,"b":2}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── PropertyNames ───────────────────────────────────────────────────

    [Test]
    public void Parse_PropertyNames_Violation()
    {
        var schema = LoadSchema("""{"type":"object","propertyNames":{"maxLength":3}}""");
        var data = Utf8("""{"longname":1}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_PropertyNames_Valid()
    {
        var schema = LoadSchema("""{"type":"object","propertyNames":{"maxLength":3}}""");
        var data = Utf8("""{"ab":1,"cd":2}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── MinProperties / MaxProperties ───────────────────────────────────

    [Test]
    public void Parse_MinPropertiesViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"object","minProperties":2}""");
        var data = Utf8("""{"a":1}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MinPropertiesExceeded);
    }

    [Test]
    public void Parse_MaxPropertiesViolation_ReturnsInvalid()
    {
        var schema = LoadSchema("""{"type":"object","maxProperties":1}""");
        var data = Utf8("""{"a":1,"b":2}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors[0].Code.Should().Be(ValidationErrorCode.MaxPropertiesExceeded);
    }

    // ── PrefixItems ─────────────────────────────────────────────────────

    [Test]
    public void Parse_PrefixItems_ValidatesPositionally()
    {
        var schema = LoadSchema("""{"type":"array","prefixItems":[{"type":"string"},{"type":"number"}]}""");
        var data = Utf8("""["hello",42]""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_PrefixItems_RejectsWrongType()
    {
        var schema = LoadSchema("""{"type":"array","prefixItems":[{"type":"string"},{"type":"number"}]}""");
        var data = Utf8("""[42,"hello"]""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    // ── Empty object/array ──────────────────────────────────────────────

    [Test]
    public void Parse_EmptyObject_Valid()
    {
        var schema = LoadSchema("""{"type":"object"}""");
        var data = Utf8("{}");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_EmptyArray_Valid()
    {
        var schema = LoadSchema("""{"type":"array"}""");
        var data = Utf8("[]");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    // ── Boolean/null scalar types ────────────────────────────────────────

    [Test]
    public void Parse_BooleanType_Valid()
    {
        var schema = LoadSchema("""{"type":"boolean"}""");
        var data = Utf8("true");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_NullType_Valid()
    {
        var schema = LoadSchema("""{"type":"null"}""");
        var data = Utf8("null");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }
}
