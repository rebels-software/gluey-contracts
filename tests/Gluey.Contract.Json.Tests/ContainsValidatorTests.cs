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
