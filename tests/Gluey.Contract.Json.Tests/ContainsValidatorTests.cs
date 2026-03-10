using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class ContainsValidatorTests
{
    // ── ValidateContains ─────────────────────────────────────────────────

    [Test]
    public void ValidateContains_ZeroMatches_DefaultMin_ReturnsFalseWithContainsInvalid()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(0, null, null, "/arr", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ContainsInvalid);
        collector[0].Path.Should().Be("/arr");
    }

    [Test]
    public void ValidateContains_OneMatch_DefaultMin_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(1, null, null, "/arr", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateContains_ZeroMatches_ExplicitMinZero_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(0, 0, null, "/arr", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateContains_ThreeMatches_WithinBounds_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(3, 2, 5, "/arr", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateContains_OneMatch_MinTwo_ReturnsFalseWithMinContainsExceeded()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(1, 2, null, "/arr", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MinContainsExceeded);
    }

    [Test]
    public void ValidateContains_SixMatches_MaxFive_ReturnsFalseWithMaxContainsExceeded()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(6, null, 5, "/arr", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MaxContainsExceeded);
    }

    [Test]
    public void ValidateContains_AtExactMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(2, 2, null, "/arr", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateContains_AtExactMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        bool result = ArrayValidator.ValidateContains(5, null, 5, "/arr", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
