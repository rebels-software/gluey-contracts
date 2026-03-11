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
using System.Text.RegularExpressions;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class PatternPropertyValidatorTests
{
    // ── ValidatePatternProperty ──────────────────────────────────────────

    [Test]
    public void ValidatePatternProperty_SchemaResultFalse_ReturnsFalseWithError()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidatePatternProperty(false, "foo", "", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.PatternPropertyInvalid);
        collector[0].Path.Should().Be("/foo");
    }

    [Test]
    public void ValidatePatternProperty_SchemaResultTrue_ReturnsTrueNoError()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidatePatternProperty(true, "foo", "", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidatePatternProperty_ChildPathEscaped_EscapesSlashInName()
    {
        using var collector = new ErrorCollector();
        ObjectValidator.ValidatePatternProperty(false, "a/b", "/obj", collector);
        collector[0].Path.Should().Be("/obj/a~1b");
    }

    // ── ValidatePropertyName ─────────────────────────────────────────────

    [Test]
    public void ValidatePropertyName_SchemaResultFalse_ReturnsFalseWithError()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidatePropertyName(false, "badName", "", collector);
        result.Should().BeFalse();
        collector.Count.Should().Be(1);
        collector[0].Code.Should().Be(ValidationErrorCode.PropertyNameInvalid);
        collector[0].Path.Should().Be("/badName");
    }

    [Test]
    public void ValidatePropertyName_SchemaResultTrue_ReturnsTrueNoError()
    {
        using var collector = new ErrorCollector();
        bool result = ObjectValidator.ValidatePropertyName(true, "goodName", "", collector);
        result.Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    // ── CompiledPatternProperties on SchemaNode ──────────────────────────

    [Test]
    public void SchemaLoad_WithPatternProperties_PopulatesCompiledPatternProperties()
    {
        var json = """{"patternProperties": {"^S_": {"type": "string"}, "^I_": {"type": "integer"}}}"""u8;
        var node = JsonSchemaLoader.Load(json);
        node.Should().NotBeNull();
        node!.CompiledPatternProperties.Should().NotBeNull();
        node.CompiledPatternProperties!.Length.Should().Be(2);
        node.CompiledPatternProperties[0].Pattern.Should().NotBeNull();
        node.CompiledPatternProperties[1].Pattern.Should().NotBeNull();
    }

    [Test]
    public void SchemaLoad_WithPatternProperties_RegexIsCompiled()
    {
        var json = """{"patternProperties": {"^foo": {"type": "string"}}}"""u8;
        var node = JsonSchemaLoader.Load(json);
        node.Should().NotBeNull();
        node!.CompiledPatternProperties![0].Pattern.Options.Should().HaveFlag(RegexOptions.Compiled);
    }

    [Test]
    public void SchemaLoad_WithInvalidRegexPattern_ReturnsNull()
    {
        var json = """{"patternProperties": {"[invalid": {"type": "string"}}}"""u8;
        var node = JsonSchemaLoader.Load(json);
        node.Should().BeNull();
    }

    [Test]
    public void SchemaLoad_WithoutPatternProperties_CompiledPatternPropertiesIsNull()
    {
        var json = """{"type": "object"}"""u8;
        var node = JsonSchemaLoader.Load(json);
        node.Should().NotBeNull();
        node!.CompiledPatternProperties.Should().BeNull();
    }
}
