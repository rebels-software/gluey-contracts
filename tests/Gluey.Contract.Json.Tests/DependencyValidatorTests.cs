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
