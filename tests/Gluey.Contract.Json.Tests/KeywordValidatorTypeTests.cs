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

    [Test]
    public void IsInteger_WholeNumber_ReturnsTrue()
    {
        KeywordValidator.IsInteger("42"u8).Should().BeTrue();
    }

    [Test]
    public void IsInteger_DecimalPointZero_ReturnsTrue()
    {
        // 1.0 is a mathematical integer per JSON Schema spec
        KeywordValidator.IsInteger("1.0"u8).Should().BeTrue();
    }

    [Test]
    public void IsInteger_FractionalValue_ReturnsFalse()
    {
        KeywordValidator.IsInteger("1.5"u8).Should().BeFalse();
    }

    [Test]
    public void IsInteger_ScientificNotationInteger_ReturnsTrue()
    {
        // 1e2 = 100, which is an integer
        KeywordValidator.IsInteger("1e2"u8).Should().BeTrue();
    }

    [Test]
    public void IsInteger_ScientificNotationWithDecimal_ReturnsTrue()
    {
        // 1.5e1 = 15, which is an integer
        KeywordValidator.IsInteger("1.5e1"u8).Should().BeTrue();
    }

    [Test]
    public void IsInteger_BeyondInt64Range_ReturnsFalse()
    {
        // Beyond Int64.MaxValue -- pragmatic limit
        KeywordValidator.IsInteger("9999999999999999999"u8).Should().BeFalse();
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
}
