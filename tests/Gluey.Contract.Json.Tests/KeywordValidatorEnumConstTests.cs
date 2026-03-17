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

using System.Text;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class KeywordValidatorEnumConstTests
{
    // ── ValidateEnum — happy paths ───────────────────────────────────────

    [Test]
    public void ValidateEnum_ByteExactMatch_Passes()
    {
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["\"hello\""u8.ToArray(), "\"world\""u8.ToArray()];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "\"hello\""u8, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateEnum_NoMatch_FailsWithError()
    {
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["\"hello\""u8.ToArray(), "\"world\""u8.ToArray()];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "\"other\""u8, false, "/val", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.EnumMismatch);
        collector[0].Path.Should().Be("/val");
    }

    [Test]
    public void ValidateEnum_NumericFallback_Passes()
    {
        // 1 matches 1.0 via numeric fallback
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["1.0"u8.ToArray()];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "1"u8, true, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateEnum_StructuredValue_ByteExactMatch_Passes()
    {
        // Structured value: {"a":1} matches enum entry {"a":1}
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["{\"a\":1}"u8.ToArray()];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "{\"a\":1}"u8, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateEnum_EmptyEnum_Fails()
    {
        // Empty enum -- nothing can match
        using var collector = new ErrorCollector();
        byte[][] enumValues = [];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "\"anything\""u8, false, "/x", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.EnumMismatch);
    }

    // ── ValidateConst — happy paths ──────────────────────────────────────

    [Test]
    public void ValidateConst_ByteExactMatch_Passes()
    {
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateConst(
            "true"u8.ToArray(), "true"u8, false, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateConst_Mismatch_FailsWithError()
    {
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateConst(
            "true"u8.ToArray(), "false"u8, false, "/flag", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ConstMismatch);
        collector[0].Path.Should().Be("/flag");
    }

    [Test]
    public void ValidateConst_NumericFallback_Passes()
    {
        // 1 matches const 1.0 via numeric fallback
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateConst(
            "1.0"u8.ToArray(), "1"u8, true, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateConst_StringMismatch_Fails()
    {
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateConst(
            "\"world\""u8.ToArray(), "\"hello\""u8, false, "/greeting", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.ConstMismatch);
    }

    // ── CheckEnum (zero-allocation) ─────────────────────────────────────

    [Test]
    public void CheckEnum_ByteExactMatch_ReturnsTrue()
    {
        byte[][] enumValues = ["\"hello\""u8.ToArray(), "\"world\""u8.ToArray()];

        KeywordValidator.CheckEnum(enumValues, "\"hello\""u8, false).Should().BeTrue();
    }

    [Test]
    public void CheckEnum_NoMatch_ReturnsFalse()
    {
        byte[][] enumValues = ["\"hello\""u8.ToArray()];

        KeywordValidator.CheckEnum(enumValues, "\"other\""u8, false).Should().BeFalse();
    }

    [Test]
    public void CheckEnum_NumericFallback_ReturnsTrue()
    {
        byte[][] enumValues = ["1.0"u8.ToArray()];

        KeywordValidator.CheckEnum(enumValues, "1"u8, true).Should().BeTrue();
    }

    [Test]
    public void CheckEnum_NumericNoMatch_ReturnsFalse()
    {
        byte[][] enumValues = ["2.0"u8.ToArray()];

        KeywordValidator.CheckEnum(enumValues, "1"u8, true).Should().BeFalse();
    }

    [Test]
    public void CheckEnum_Empty_ReturnsFalse()
    {
        byte[][] enumValues = [];

        KeywordValidator.CheckEnum(enumValues, "42"u8, true).Should().BeFalse();
    }

    // ── CheckConst (zero-allocation) ────────────────────────────────────

    [Test]
    public void CheckConst_ByteExactMatch_ReturnsTrue()
    {
        KeywordValidator.CheckConst("true"u8.ToArray(), "true"u8, false).Should().BeTrue();
    }

    [Test]
    public void CheckConst_Mismatch_ReturnsFalse()
    {
        KeywordValidator.CheckConst("true"u8.ToArray(), "false"u8, false).Should().BeFalse();
    }

    [Test]
    public void CheckConst_NumericFallback_ReturnsTrue()
    {
        KeywordValidator.CheckConst("1.0"u8.ToArray(), "1"u8, true).Should().BeTrue();
    }

    [Test]
    public void CheckConst_NumericMismatch_ReturnsFalse()
    {
        KeywordValidator.CheckConst("2.0"u8.ToArray(), "1"u8, true).Should().BeFalse();
    }

    // ── TryNumericEqual ─────────────────────────────────────────────────

    [Test]
    public void TryNumericEqual_EqualDecimals_ReturnsTrue()
    {
        bool parsed = KeywordValidator.TryNumericEqual("1.0"u8, "1"u8, out bool equal);

        parsed.Should().BeTrue();
        equal.Should().BeTrue();
    }

    [Test]
    public void TryNumericEqual_DifferentValues_ReturnsFalseEqual()
    {
        bool parsed = KeywordValidator.TryNumericEqual("1"u8, "2"u8, out bool equal);

        parsed.Should().BeTrue();
        equal.Should().BeFalse();
    }

    [Test]
    public void TryNumericEqual_LargeDecimals_Compares()
    {
        bool parsed = KeywordValidator.TryNumericEqual("123456789.0"u8, "123456789"u8, out bool equal);

        parsed.Should().BeTrue();
        equal.Should().BeTrue();
    }

    [Test]
    public void TryNumericEqual_ScientificNotation_MatchesPlain()
    {
        bool parsed = KeywordValidator.TryNumericEqual("1e2"u8, "100"u8, out bool equal);

        parsed.Should().BeTrue();
        equal.Should().BeTrue();
    }

    // ── ValidateEnum with numeric non-match in byte pass ────────────────

    [Test]
    public void ValidateEnum_NumericByteMatch_Passes()
    {
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["10"u8.ToArray(), "20"u8.ToArray()];

        bool result = KeywordValidator.ValidateEnum(
            enumValues, "10"u8, true, "", collector);

        result.Should().BeTrue();
    }

    [Test]
    public void ValidateEnum_NumericFallbackMatch_Passes()
    {
        using var collector = new ErrorCollector();
        byte[][] enumValues = ["1e2"u8.ToArray()];

        // "100" doesn't byte-match "1e2", but numeric fallback should match
        bool result = KeywordValidator.ValidateEnum(
            enumValues, "100"u8, true, "", collector);

        result.Should().BeTrue();
    }
}
