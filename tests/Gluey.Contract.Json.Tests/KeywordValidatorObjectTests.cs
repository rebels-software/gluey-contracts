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
public class KeywordValidatorObjectTests
{
    // ── ValidateRequired — happy paths ───────────────────────────────────

    [Test]
    public void ValidateRequired_AllPresent_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string> { "name", "age" };

        bool result = KeywordValidator.ValidateRequired(
            new[] { "name", "age" }, seen, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateRequired_EmptyRequiredArray_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string> { "name" };

        bool result = KeywordValidator.ValidateRequired(
            Array.Empty<string>(), seen, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    // ── ValidateRequired — failure paths ─────────────────────────────────

    [Test]
    public void ValidateRequired_OneMissing_ReturnsFalseWithOneError()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string> { "name" };

        bool result = KeywordValidator.ValidateRequired(
            new[] { "name", "age" }, seen, "", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.RequiredMissing);
        collector[0].Path.Should().Be("/age");
    }

    [Test]
    public void ValidateRequired_MultipleMissing_CollectsAllErrors()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string>();

        bool result = KeywordValidator.ValidateRequired(
            new[] { "name", "age", "email" }, seen, "", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(3);
        collector[0].Code.Should().Be(ValidationErrorCode.RequiredMissing);
        collector[0].Path.Should().Be("/name");
        collector[1].Path.Should().Be("/age");
        collector[2].Path.Should().Be("/email");
    }

    [Test]
    public void ValidateRequired_SpecialCharsInName_UsesRfc6901Escaping()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string>();

        // ~ -> ~0, / -> ~1
        bool result = KeywordValidator.ValidateRequired(
            new[] { "a/b", "c~d" }, seen, "", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(2);
        collector[0].Path.Should().Be("/a~1b");
        collector[1].Path.Should().Be("/c~0d");
    }

    [Test]
    public void ValidateRequired_NestedPath_BuildsCorrectChildPath()
    {
        using var collector = new ErrorCollector();
        var seen = new HashSet<string>();

        bool result = KeywordValidator.ValidateRequired(
            new[] { "street" }, seen, "/address", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Path.Should().Be("/address/street");
    }

    // ── ValidateAdditionalProperty — allowed cases ───────────────────────

    [Test]
    public void ValidateAdditionalProperty_KnownProperty_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var properties = new Dictionary<string, SchemaNode>
        {
            ["name"] = new SchemaNode("")
        };

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "name", properties, null, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateAdditionalProperty_UnknownProperty_AdditionalNull_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var properties = new Dictionary<string, SchemaNode>
        {
            ["name"] = new SchemaNode("")
        };

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "unknown", properties, null, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateAdditionalProperty_UnknownProperty_AdditionalTrue_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var properties = new Dictionary<string, SchemaNode>
        {
            ["name"] = new SchemaNode("")
        };

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "unknown", properties, SchemaNode.True, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateAdditionalProperty_UnknownProperty_AdditionalSchemaNode_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        var properties = new Dictionary<string, SchemaNode>
        {
            ["name"] = new SchemaNode("")
        };
        var additionalSchema = new SchemaNode("", type: SchemaType.String);

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "unknown", properties, additionalSchema, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    [Test]
    public void ValidateAdditionalProperty_NullProperties_NullAdditional_ReturnsTrue()
    {
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "anything", null, null, "", collector);

        result.Should().BeTrue();
        collector.HasErrors.Should().BeFalse();
    }

    // ── ValidateAdditionalProperty — rejection cases ─────────────────────

    [Test]
    public void ValidateAdditionalProperty_UnknownProperty_AdditionalFalse_ReturnsFalse()
    {
        using var collector = new ErrorCollector();
        var properties = new Dictionary<string, SchemaNode>
        {
            ["name"] = new SchemaNode("")
        };

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "unknown", properties, SchemaNode.False, "", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.AdditionalPropertyNotAllowed);
        collector[0].Path.Should().Be("/unknown");
    }

    [Test]
    public void ValidateAdditionalProperty_NullProperties_AdditionalFalse_ReturnsFalse()
    {
        using var collector = new ErrorCollector();

        bool result = KeywordValidator.ValidateAdditionalProperty(
            "anything", null, SchemaNode.False, "", collector);

        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.AdditionalPropertyNotAllowed);
        collector[0].Path.Should().Be("/anything");
    }
}
