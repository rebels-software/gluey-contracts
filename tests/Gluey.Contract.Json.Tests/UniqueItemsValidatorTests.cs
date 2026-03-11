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

using System.Text;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class UniqueItemsValidatorTests
{
    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Empty and single element ────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_EmptyArray_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var elements = Array.Empty<byte[]>();
        var isNumber = Array.Empty<bool>();
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateUniqueItems_SingleElement_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1") };
        var isNumber = new[] { true };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── Distinct elements ───────────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_DistinctNumbers_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1"), Utf8("2"), Utf8("3") };
        var isNumber = new[] { true, true, true };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── Byte-exact duplicates ───────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_DuplicateNumbers_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1"), Utf8("1") };
        var isNumber = new[] { true, true };
        bool result = ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
        collector[0].Path.Should().Be("/arr");
    }

    // ── Numeric equivalence ─────────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_IntegerAndDecimalEquivalent_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1"), Utf8("1.0") };
        var isNumber = new[] { true, true };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
    }

    [Test]
    public void ValidateUniqueItems_ScientificNotationEquivalent_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1e2"), Utf8("100") };
        var isNumber = new[] { true, true };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
    }

    // ── String duplicates ───────────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_DuplicateStrings_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("\"a\""), Utf8("\"b\""), Utf8("\"a\"") };
        var isNumber = new[] { false, false, false };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
    }

    // ── Boolean duplicates ──────────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_DuplicateBooleans_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("true"), Utf8("false"), Utf8("true") };
        var isNumber = new[] { false, false, false };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
    }

    // ── Null duplicates ─────────────────────────────────────────────────

    [Test]
    public void ValidateUniqueItems_DuplicateNulls_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("null"), Utf8("null") };
        var isNumber = new[] { false, false };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.UniqueItemsViolation);
    }

    // ── Different types are not duplicates ───────────────────────────────

    [Test]
    public void ValidateUniqueItems_NumberAndStringOfSameChars_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var elements = new[] { Utf8("1"), Utf8("\"1\"") };
        var isNumber = new[] { true, false };
        ArrayValidator.ValidateUniqueItems(elements, isNumber, "/arr", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
