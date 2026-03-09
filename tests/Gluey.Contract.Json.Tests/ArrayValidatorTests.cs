using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class ArrayValidatorTests
{
    // ── ValidateMinItems ──────────────────────────────────────────────────

    [Test]
    public void ValidateMinItems_CountAboveMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMinItems(3, 2, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinItems_CountAtMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMinItems(2, 2, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinItems_CountBelowMinimum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateMinItems(1, 2, "/tags", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MinItemsExceeded);
        collector[0].Path.Should().Be("/tags");
    }

    [Test]
    public void ValidateMinItems_ZeroCountZeroMin_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMinItems(0, 0, "/items", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── ValidateMaxItems ──────────────────────────────────────────────────

    [Test]
    public void ValidateMaxItems_CountBelowMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMaxItems(2, 3, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxItems_CountAtMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMaxItems(3, 3, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxItems_CountAboveMaximum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateMaxItems(4, 3, "/tags", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MaxItemsExceeded);
        collector[0].Path.Should().Be("/tags");
    }

    [Test]
    public void ValidateMaxItems_ZeroCountZeroMax_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMaxItems(0, 0, "/items", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
