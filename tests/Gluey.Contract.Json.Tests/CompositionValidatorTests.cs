using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class CompositionValidatorTests
{
    // ── ValidateAllOf ──────────────────────────────────────────────────

    [Test]
    public void ValidateAllOf_AllPass_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateAllOf(3, 3, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateAllOf_SomeFail_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = CompositionValidator.ValidateAllOf(2, 3, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.AllOfInvalid);
        collector[0].Path.Should().Be("/root");
    }

    [Test]
    public void ValidateAllOf_NoneFail_ZeroTotal_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateAllOf(0, 0, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── ValidateAnyOf ──────────────────────────────────────────────────

    [Test]
    public void ValidateAnyOf_OnePass_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateAnyOf(1, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateAnyOf_MultiplePass_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateAnyOf(3, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateAnyOf_NonePass_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = CompositionValidator.ValidateAnyOf(0, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.AnyOfInvalid);
        collector[0].Path.Should().Be("/root");
    }

    // ── ValidateOneOf ──────────────────────────────────────────────────

    [Test]
    public void ValidateOneOf_ExactlyOne_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateOneOf(1, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateOneOf_Zero_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = CompositionValidator.ValidateOneOf(0, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.OneOfInvalid);
        collector[0].Path.Should().Be("/root");
    }

    [Test]
    public void ValidateOneOf_Multiple_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = CompositionValidator.ValidateOneOf(2, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.OneOfInvalid);
        collector[0].Path.Should().Be("/root");
    }

    // ── ValidateNot ────────────────────────────────────────────────────

    [Test]
    public void ValidateNot_SubschemaFails_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        CompositionValidator.ValidateNot(false, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateNot_SubschemaPasses_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = CompositionValidator.ValidateNot(true, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.NotInvalid);
        collector[0].Path.Should().Be("/root");
    }
}
