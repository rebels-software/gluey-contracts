using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class ObjectValidatorTests
{
    // ── ValidateMinProperties ─────────────────────────────────────────────

    [Test]
    public void ValidateMinProperties_CountAboveMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMinProperties(3, 2, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinProperties_CountAtMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMinProperties(2, 2, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinProperties_CountBelowMinimum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidateMinProperties(1, 2, "/obj", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MinPropertiesExceeded);
        collector[0].Path.Should().Be("/obj");
    }

    [Test]
    public void ValidateMinProperties_ZeroCountZeroMin_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMinProperties(0, 0, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── ValidateMaxProperties ─────────────────────────────────────────────

    [Test]
    public void ValidateMaxProperties_CountBelowMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMaxProperties(2, 3, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxProperties_CountAtMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMaxProperties(3, 3, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxProperties_CountAboveMaximum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidateMaxProperties(4, 3, "/obj", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MaxPropertiesExceeded);
        collector[0].Path.Should().Be("/obj");
    }

    [Test]
    public void ValidateMaxProperties_ZeroCountZeroMax_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidateMaxProperties(0, 0, "/obj", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
