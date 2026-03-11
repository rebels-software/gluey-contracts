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
public class ConditionalValidatorTests
{
    // ── ValidateIfThen ──────────────────────────────────────────────────

    [Test]
    public void ValidateIfThen_ThenPassed_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ConditionalValidator.ValidateIfThen(true, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateIfThen_ThenFailed_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ConditionalValidator.ValidateIfThen(false, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.IfThenInvalid);
        collector[0].Path.Should().Be("/root");
    }

    // ── ValidateIfElse ──────────────────────────────────────────────────

    [Test]
    public void ValidateIfElse_ElsePassed_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ConditionalValidator.ValidateIfElse(true, "/root", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateIfElse_ElseFailed_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        bool result = ConditionalValidator.ValidateIfElse(false, "/root", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.IfElseInvalid);
        collector[0].Path.Should().Be("/root");
    }
}
