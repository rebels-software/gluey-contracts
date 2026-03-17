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
public class KeywordValidatorTypeTests
{
    // ── MapTokenToSchemaType ─────────────────────────────────────────────

    [Test]
    public void MapTokenToSchemaType_NullToken_ReturnsNull()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.Null, false)
            .Should().Be(SchemaType.Null);
    }

    [Test]
    public void MapTokenToSchemaType_TrueToken_ReturnsBoolean()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.True, false)
            .Should().Be(SchemaType.Boolean);
    }

    [Test]
    public void MapTokenToSchemaType_FalseToken_ReturnsBoolean()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.False, false)
            .Should().Be(SchemaType.Boolean);
    }

    [Test]
    public void MapTokenToSchemaType_StringToken_ReturnsString()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.String, false)
            .Should().Be(SchemaType.String);
    }

    [Test]
    public void MapTokenToSchemaType_StartObjectToken_ReturnsObject()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.StartObject, false)
            .Should().Be(SchemaType.Object);
    }

    [Test]
    public void MapTokenToSchemaType_StartArrayToken_ReturnsArray()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.StartArray, false)
            .Should().Be(SchemaType.Array);
    }

    [Test]
    public void MapTokenToSchemaType_NumberNotInteger_ReturnsNumber()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.Number, false)
            .Should().Be(SchemaType.Number);
    }

    [Test]
    public void MapTokenToSchemaType_NumberIsInteger_ReturnsIntegerAndNumber()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.Number, true)
            .Should().Be(SchemaType.Integer | SchemaType.Number);
    }

    [Test]
    public void MapTokenToSchemaType_NoneToken_ReturnsNone()
    {
        KeywordValidator.MapTokenToSchemaType(JsonByteTokenType.None, false)
            .Should().Be(SchemaType.None);
    }

    // ── IsInteger ────────────────────────────────────────────────────────

    [TestCase("42", true)]
    [TestCase("0", true)]
    [TestCase("-42", true)]
    [TestCase("1.0", true)]          // mathematical integer per JSON Schema spec
    [TestCase("1e2", true)]          // 100
    [TestCase("1.5e1", true)]        // 15
    [TestCase("9223372036854775807", true)] // Int64.MaxValue
    [TestCase("1.5", false)]
    [TestCase("-1.5", false)]
    [TestCase("1.5e0", false)]       // 1.5, not integer
    [TestCase("9999999999999999999", false)] // beyond Int64 range
    public void IsInteger_ReturnsExpected(string input, bool expected)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        KeywordValidator.IsInteger(bytes).Should().Be(expected);
    }

    [Test]
    public void IsInteger_NotANumber_ReturnsFalse()
    {
        KeywordValidator.IsInteger("\"text\""u8).Should().BeFalse();
    }

    // ── ValidateType — happy paths ───────────────────────────────────────

    [Test]
    public void ValidateType_NullToken_NullType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Null, JsonByteTokenType.Null, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_TrueToken_BooleanType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Boolean, JsonByteTokenType.True, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_FalseToken_BooleanType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Boolean, JsonByteTokenType.False, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_StringToken_StringType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.String, JsonByteTokenType.String, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_NumberToken_NumberType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Number, JsonByteTokenType.Number, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_IntegerToken_IntegerType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Integer, JsonByteTokenType.Number, true, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_IntegerToken_NumberType_Passes()
    {
        // Integer satisfies number per JSON Schema spec
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Number, JsonByteTokenType.Number, true, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_StartObjectToken_ObjectType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Object, JsonByteTokenType.StartObject, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateType_StartArrayToken_ArrayType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Array, JsonByteTokenType.StartArray, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    // ── ValidateType — failure paths ─────────────────────────────────────

    [Test]
    public void ValidateType_StringToken_IntegerType_FailsWithError()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Integer, JsonByteTokenType.String, false, "/name", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.TypeMismatch);
        collector[0].Path.Should().Be("/name");
    }

    // ── ValidateType — multi-type ────────────────────────────────────────

    [Test]
    public void ValidateType_NumberToken_StringOrNumberType_Passes()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.String | SchemaType.Number,
            JsonByteTokenType.Number, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    // ── ValidateType — error accumulation (VALD-17) ──────────────────────

    [Test]
    public void ValidateType_MultipleFailures_AccumulateInCollector()
    {
        using var collector = new ErrorCollector();

        // First failure
        KeywordValidator.ValidateType(
            SchemaType.Integer, JsonByteTokenType.String, false, "/a", collector);

        // Second failure
        KeywordValidator.ValidateType(
            SchemaType.Boolean, JsonByteTokenType.Number, false, "/b", collector);

        collector.Count.Should().Be(2);
        collector[0].Path.Should().Be("/a");
        collector[1].Path.Should().Be("/b");
    }

    // ── CheckType (zero-allocation) ─────────────────────────────────────

    [Test]
    public void CheckType_Match_ReturnsTrue()
    {
        KeywordValidator.CheckType(SchemaType.String, JsonByteTokenType.String, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_Mismatch_ReturnsFalse()
    {
        KeywordValidator.CheckType(SchemaType.String, JsonByteTokenType.Number, false)
            .Should().BeFalse();
    }

    [Test]
    public void CheckType_IntegerMatchesNumber_ReturnsTrue()
    {
        KeywordValidator.CheckType(SchemaType.Number, JsonByteTokenType.Number, true)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_IntegerMatchesInteger_ReturnsTrue()
    {
        KeywordValidator.CheckType(SchemaType.Integer, JsonByteTokenType.Number, true)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_NonIntegerNumber_DoesNotMatchInteger()
    {
        KeywordValidator.CheckType(SchemaType.Integer, JsonByteTokenType.Number, false)
            .Should().BeFalse();
    }

    [Test]
    public void CheckType_MultiType_MatchesAny()
    {
        KeywordValidator.CheckType(SchemaType.String | SchemaType.Null, JsonByteTokenType.Null, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_NoneToken_MatchesNothing()
    {
        KeywordValidator.CheckType(SchemaType.String, JsonByteTokenType.None, false)
            .Should().BeFalse();
    }

    // ── ValidateType: all token types against wrong schema type ─────

    [Test]
    public void ValidateType_ObjectToken_StringType_Fails()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.String, JsonByteTokenType.StartObject, false, "/obj", collector);

        result.Should().BeFalse();
        collector[0].Code.Should().Be(ValidationErrorCode.TypeMismatch);
    }

    [Test]
    public void ValidateType_ArrayToken_ObjectType_Fails()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Object, JsonByteTokenType.StartArray, false, "/arr", collector);

        result.Should().BeFalse();
    }

    [Test]
    public void ValidateType_NullToken_StringType_Fails()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.String, JsonByteTokenType.Null, false, "/n", collector);

        result.Should().BeFalse();
    }

    [Test]
    public void ValidateType_BoolToken_NumberType_Fails()
    {
        using var collector = new ErrorCollector();
        bool result = KeywordValidator.ValidateType(
            SchemaType.Number, JsonByteTokenType.True, false, "/b", collector);

        result.Should().BeFalse();
    }

    // ── CheckType: all combinations ─────────────────────────────────

    [Test]
    public void CheckType_ObjectToken_ObjectType_Matches()
    {
        KeywordValidator.CheckType(SchemaType.Object, JsonByteTokenType.StartObject, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_ArrayToken_ArrayType_Matches()
    {
        KeywordValidator.CheckType(SchemaType.Array, JsonByteTokenType.StartArray, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_NullToken_NullType_Matches()
    {
        KeywordValidator.CheckType(SchemaType.Null, JsonByteTokenType.Null, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_TrueToken_BooleanType_Matches()
    {
        KeywordValidator.CheckType(SchemaType.Boolean, JsonByteTokenType.True, false)
            .Should().BeTrue();
    }

    [Test]
    public void CheckType_FalseToken_BooleanType_Matches()
    {
        KeywordValidator.CheckType(SchemaType.Boolean, JsonByteTokenType.False, false)
            .Should().BeTrue();
    }
}
