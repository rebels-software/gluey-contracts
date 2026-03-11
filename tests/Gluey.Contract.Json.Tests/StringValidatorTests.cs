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
using System.Text.RegularExpressions;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class StringValidatorTests
{
    // ── CountCodepoints ──────────────────────────────────────────────────

    [Test]
    public void CountCodepoints_AsciiString_ReturnsLength()
    {
        var bytes = "hello"u8;
        StringValidator.CountCodepoints(bytes).Should().Be(5);
    }

    [Test]
    public void CountCodepoints_EmptyBytes_ReturnsZero()
    {
        StringValidator.CountCodepoints(ReadOnlySpan<byte>.Empty).Should().Be(0);
    }

    [Test]
    public void CountCodepoints_Emoji_ReturnsOne()
    {
        // U+1F600 grinning face = 4 UTF-8 bytes, 1 codepoint
        var bytes = Encoding.UTF8.GetBytes("\U0001F600");
        StringValidator.CountCodepoints(bytes).Should().Be(1);
    }

    [Test]
    public void CountCodepoints_CombiningCharacter_CountsEachCodepoint()
    {
        // "cafe\u0301" = 'c','a','f','e' + combining acute accent = 5 codepoints
        var bytes = Encoding.UTF8.GetBytes("cafe\u0301");
        StringValidator.CountCodepoints(bytes).Should().Be(5);
    }

    [Test]
    public void CountCodepoints_CjkCharacter_ReturnsOne()
    {
        // U+4E2D (CJK character, 3 UTF-8 bytes, 1 codepoint)
        var bytes = Encoding.UTF8.GetBytes("\u4E2D");
        StringValidator.CountCodepoints(bytes).Should().Be(1);
    }

    [Test]
    public void CountCodepoints_MixedAsciiAndMultiByte_CorrectCount()
    {
        // "A" (1 byte) + U+00E9 (2 bytes) + U+1F600 (4 bytes) = 3 codepoints
        var bytes = Encoding.UTF8.GetBytes("A\u00E9\U0001F600");
        StringValidator.CountCodepoints(bytes).Should().Be(3);
    }

    // ── ValidateMinLength ────────────────────────────────────────────────

    [Test]
    public void ValidateMinLength_CountAboveMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMinLength(5, 3, "/name", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinLength_CountAtMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMinLength(3, 3, "/name", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinLength_CountBelowMinimum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMinLength(2, 3, "/name", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MinLengthExceeded);
    }

    // ── ValidateMaxLength ────────────────────────────────────────────────

    [Test]
    public void ValidateMaxLength_CountBelowMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMaxLength(3, 5, "/name", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxLength_CountAtMaximum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMaxLength(5, 5, "/name", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMaxLength_CountAboveMaximum_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        StringValidator.ValidateMaxLength(6, 5, "/name", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.MaxLengthExceeded);
    }

    // ── ValidatePattern ──────────────────────────────────────────────────

    [Test]
    public void ValidatePattern_MatchingValue_ReturnsTrue()
    {
        var regex = new Regex(@"^[a-z]+\d+$", RegexOptions.Compiled);
        using var collector = new ErrorCollector();
        StringValidator.ValidatePattern("abc123", regex, "/code", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidatePattern_NonMatchingValue_ReturnsFalse()
    {
        var regex = new Regex(@"^[a-z]+$", RegexOptions.Compiled);
        using var collector = new ErrorCollector();
        StringValidator.ValidatePattern("ABC", regex, "/code", collector).Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.PatternMismatch);
    }

    // ── CompiledPattern on SchemaNode via JsonSchemaLoader ───────────────

    [Test]
    public void Load_SchemaWithPattern_SetsCompiledPattern()
    {
        var json = """{"type":"string","pattern":"^\\d+$"}"""u8;
        var node = JsonSchemaLoader.Load(json);

        node.Should().NotBeNull();
        node!.Pattern.Should().Be(@"^\d+$");
        node.CompiledPattern.Should().NotBeNull();
        node.CompiledPattern!.IsMatch("123").Should().BeTrue();
        node.CompiledPattern.IsMatch("abc").Should().BeFalse();
    }

    [Test]
    public void Load_SchemaWithoutPattern_CompiledPatternIsNull()
    {
        var json = """{"type":"string"}"""u8;
        var node = JsonSchemaLoader.Load(json);

        node.Should().NotBeNull();
        node!.CompiledPattern.Should().BeNull();
    }

    [Test]
    public void Load_SchemaWithInvalidPattern_ReturnsNull()
    {
        var json = """{"type":"string","pattern":"[invalid"}"""u8;
        var node = JsonSchemaLoader.Load(json);

        node.Should().BeNull();
    }
}
