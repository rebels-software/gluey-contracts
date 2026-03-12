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
public class NumericValidatorTests
{
    // ── TryParseDecimal ──────────────────────────────────────────────────

    [Test]
    public void TryParseDecimal_ValidInteger_ReturnsTrue()
    {
        var bytes = "42"u8;
        NumericValidator.TryParseDecimal(bytes, out decimal value).Should().BeTrue();
        value.Should().Be(42m);
    }

    [Test]
    public void TryParseDecimal_ValidDecimal_ReturnsTrue()
    {
        var bytes = "3.14"u8;
        NumericValidator.TryParseDecimal(bytes, out decimal value).Should().BeTrue();
        value.Should().Be(3.14m);
    }

    [Test]
    public void TryParseDecimal_InvalidBytes_ReturnsFalse()
    {
        var bytes = "\"abc\""u8;
        NumericValidator.TryParseDecimal(bytes, out _).Should().BeFalse();
    }

    [Test]
    public void TryParseDecimal_NegativeNumber_ReturnsTrue()
    {
        var bytes = "-7.5"u8;
        NumericValidator.TryParseDecimal(bytes, out decimal value).Should().BeTrue();
        value.Should().Be(-7.5m);
    }

    // ── ValidateMinimum ──────────────────────────────────────────────────

    [Test]
    public void ValidateMinimum_ValueAboveMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMinimum(5m, 3m, "/age", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinimum_ValueAtMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMinimum(3m, 3m, "/age", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinimum_ValueBelowMinimum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMinimum(2m, 3m, "/age", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MinimumExceeded);
    }

    [Test]
    public void ValidateMinimum_DecimalPrecision_ValueEquals()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMinimum(1.0m, 1.0m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── ValidateMaximum ──────────────────────────────────────────────────

    [Test]
    public void ValidateMaximum_ValueBelowMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMaximum(3m, 5m, "/age", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaximum_ValueAtMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMaximum(5m, 5m, "/age", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaximum_ValueAboveMaximum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMaximum(6m, 5m, "/age", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MaximumExceeded);
    }

    // ── ValidateExclusiveMinimum ─────────────────────────────────────────

    [Test]
    public void ValidateExclusiveMinimum_ValueAboveBoundary_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMinimum(4m, 3m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateExclusiveMinimum_ValueAtBoundary_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMinimum(3m, 3m, "/val", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ExclusiveMinimumExceeded);
    }

    [Test]
    public void ValidateExclusiveMinimum_ValueBelowBoundary_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMinimum(2m, 3m, "/val", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ExclusiveMinimumExceeded);
    }

    // ── ValidateExclusiveMaximum ─────────────────────────────────────────

    [Test]
    public void ValidateExclusiveMaximum_ValueBelowBoundary_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMaximum(4m, 5m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateExclusiveMaximum_ValueAtBoundary_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMaximum(5m, 5m, "/val", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ExclusiveMaximumExceeded);
    }

    [Test]
    public void ValidateExclusiveMaximum_ValueAboveBoundary_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateExclusiveMaximum(6m, 5m, "/val", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ExclusiveMaximumExceeded);
    }

    // ── ValidateMultipleOf ───────────────────────────────────────────────

    [Test]
    public void ValidateMultipleOf_ValueIsMultiple_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMultipleOf(10m, 5m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMultipleOf_ValueIsNotMultiple_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMultipleOf(7m, 3m, "/val", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MultipleOfInvalid);
    }

    [Test]
    public void ValidateMultipleOf_DecimalPrecision_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMultipleOf(0.3m, 0.1m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMultipleOf_DecimalMultiple_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMultipleOf(4.5m, 1.5m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMultipleOf_ZeroDivisor_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        NumericValidator.ValidateMultipleOf(10m, 0m, "/val", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
