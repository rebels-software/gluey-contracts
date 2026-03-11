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
