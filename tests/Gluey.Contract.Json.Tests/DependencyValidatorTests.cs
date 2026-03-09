using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class DependencyValidatorTests
{
    // ── ValidateDependentRequired ────────────────────────────────────────

    [Test]
    public void ValidateDependentRequired_TriggerPresent_AllDependentsPresent_ReturnsTrue()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress" }
        };
        var present = new HashSet<string> { "creditCard", "billingAddress" };
        using var collector = new ErrorCollector();

        DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateDependentRequired_TriggerPresent_OneDependentMissing_ReturnsFalse()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress" }
        };
        var present = new HashSet<string> { "creditCard" };
        using var collector = new ErrorCollector();

        bool result = DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.DependentRequiredMissing);
        collector[0].Path.Should().Be("/root");
    }

    [Test]
    public void ValidateDependentRequired_TriggerPresent_TwoDependentsMissing_CollectsAll()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress", "securityCode" }
        };
        var present = new HashSet<string> { "creditCard" };
        using var collector = new ErrorCollector();

        bool result = DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(2);
        collector[0].Code.Should().Be(ValidationErrorCode.DependentRequiredMissing);
        collector[1].Code.Should().Be(ValidationErrorCode.DependentRequiredMissing);
    }

    [Test]
    public void ValidateDependentRequired_TriggerAbsent_ReturnsTrue()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress" }
        };
        var present = new HashSet<string> { "name" };
        using var collector = new ErrorCollector();

        DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateDependentRequired_MultipleTriggers_OneActiveWithMissing_ReturnsFalse()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress" },
            ["coupon"] = new[] { "couponCode" }
        };
        var present = new HashSet<string> { "creditCard", "billingAddress" }; // coupon absent
        using var collector = new ErrorCollector();

        DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector)
            .Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateDependentRequired_MultipleTriggers_OneActiveMissing_ReturnsFalse()
    {
        var deps = new Dictionary<string, string[]>
        {
            ["creditCard"] = new[] { "billingAddress" },
            ["coupon"] = new[] { "couponCode" }
        };
        // creditCard present but billingAddress missing; coupon absent (skipped)
        var present = new HashSet<string> { "creditCard" };
        using var collector = new ErrorCollector();

        bool result = DependencyValidator.ValidateDependentRequired(deps, present, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.DependentRequiredMissing);
        collector[0].Path.Should().Be("/root");
    }

    // ── ValidateDependentSchema ─────────────────────────────────────────

    [Test]
    public void ValidateDependentSchema_SchemaPassed_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        DependencyValidator.ValidateDependentSchema(true, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateDependentSchema_SchemaFailed_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = DependencyValidator.ValidateDependentSchema(false, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.DependentSchemaInvalid);
        collector[0].Path.Should().Be("/root");
    }
}
