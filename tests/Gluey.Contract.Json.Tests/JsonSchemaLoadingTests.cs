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
public class JsonSchemaLoadingTests
{
    // ── TryLoad API ───────────────────────────────────────────────────

    [Test]
    public void TryLoad_FromBytes_ValidSchema_ReturnsTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"type":"object","properties":{"name":{"type":"string"}}}""");

        var success = JsonContractSchema.TryLoad(bytes, out var schema);

        success.Should().BeTrue();
        schema.Should().NotBeNull();
    }

    [Test]
    public void TryLoad_FromString_ValidSchema_ReturnsTrue()
    {
        var success = JsonContractSchema.TryLoad(
            """{"type":"object","properties":{"name":{"type":"string"}}}""",
            out var schema);

        success.Should().BeTrue();
        schema.Should().NotBeNull();
    }

    [Test]
    public void Load_FromBytes_ValidSchema_ReturnsNonNull()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"type":"object","properties":{"name":{"type":"string"}}}""");

        var schema = JsonContractSchema.Load(bytes);

        schema.Should().NotBeNull();
    }

    [Test]
    public void Load_FromString_ValidSchema_ReturnsNonNull()
    {
        var schema = JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}}}""");

        schema.Should().NotBeNull();
    }

    // ── Invalid input ─────────────────────────────────────────────────

    [Test]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("")]
    public void TryLoad_InvalidJson_ReturnsFalse(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        var success = JsonContractSchema.TryLoad(bytes, out var schema);

        success.Should().BeFalse();
        schema.Should().BeNull();
    }

    [Test]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("null")]
    [TestCase("")]
    public void Load_InvalidJson_ReturnsNull(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        var result = JsonContractSchema.Load(bytes);

        result.Should().BeNull();
    }

    // ── PropertyCount ─────────────────────────────────────────────────

    [Test]
    public void PropertyCount_MatchesNamedProperties()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" },
                "email": { "type": "string" }
            }
        }
        """);

        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(3);
    }

    [Test]
    public void PropertyCount_NestedProperties_IncludesAllNamed()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" }
                    }
                }
            }
        }
        """);

        // name=0, address=1, street=2, city=3 -> 4 properties
        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(4);
    }

    // ── Ordinal assignment ────────────────────────────────────────────

    [Test]
    public void SchemaIndexer_AssignsDepthFirstOrdinals()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" }
                    }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal["/name"].Should().Be(0);
        nameToOrdinal["/address"].Should().Be(1);
        nameToOrdinal["/address/street"].Should().Be(2);
        nameToOrdinal["/address/city"].Should().Be(3);
        count.Should().Be(4);
    }

    [Test]
    public void SchemaIndexer_CompositionSubSchemas_AlsoGetOrdinals()
    {
        var json = """
        {
            "allOf": [
                {
                    "properties": {
                        "name": { "type": "string" }
                    }
                },
                {
                    "properties": {
                        "age": { "type": "integer" }
                    }
                }
            ]
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/name");
        nameToOrdinal.Should().ContainKey("/age");
        count.Should().Be(2);
    }

    // ── Boolean schema handling ───────────────────────────────────────

    [Test]
    public void BooleanSchema_AdditionalPropertiesFalse_LoadsSuccessfully()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" }
            },
            "additionalProperties": false
        }
        """);

        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(1);
    }

    // ── Unknown keywords ──────────────────────────────────────────────

    [Test]
    public void UnknownKeywords_Ignored_LoadsSuccessfully()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "object",
            "x-custom": 42,
            "properties": {
                "name": { "type": "string" }
            }
        }
        """);

        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(1);
    }

    // ── SchemaIndexer: conditional/composition property ordinals ─────

    [Test]
    public void SchemaIndexer_IfThenElse_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "object",
            "if": { "properties": { "kind": { "type": "string" } } },
            "then": { "properties": { "value": { "type": "number" } } },
            "else": { "properties": { "fallback": { "type": "string" } } }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/kind");
        nameToOrdinal.Should().ContainKey("/value");
        nameToOrdinal.Should().ContainKey("/fallback");
        count.Should().Be(3);
    }

    [Test]
    public void SchemaIndexer_NotSchema_PropertiesGetOrdinals()
    {
        var json = """
        {
            "not": {
                "properties": {
                    "forbidden": { "type": "string" }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/forbidden");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_Items_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "id": { "type": "integer" }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/id");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_PrefixItems_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "array",
            "prefixItems": [
                { "type": "object", "properties": { "a": { "type": "string" } } },
                { "type": "object", "properties": { "b": { "type": "number" } } }
            ]
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/a");
        nameToOrdinal.Should().ContainKey("/b");
        count.Should().Be(2);
    }

    [Test]
    public void SchemaIndexer_Contains_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "array",
            "contains": {
                "type": "object",
                "properties": {
                    "tag": { "type": "string" }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/tag");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_AdditionalProperties_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "object",
            "additionalProperties": {
                "type": "object",
                "properties": {
                    "nested": { "type": "string" }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/nested");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_PatternProperties_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "object",
            "patternProperties": {
                "^s": {
                    "type": "object",
                    "properties": {
                        "inner": { "type": "string" }
                    }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/^s/inner");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_DependentSchemas_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "object",
            "dependentSchemas": {
                "a": {
                    "properties": {
                        "extra": { "type": "string" }
                    }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/a/extra");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_PropertyNames_PropertiesGetOrdinals()
    {
        var json = """
        {
            "type": "object",
            "propertyNames": {
                "type": "object",
                "properties": {
                    "meta": { "type": "string" }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/meta");
        count.Should().Be(1);
    }

    [Test]
    public void SchemaIndexer_Defs_PropertiesGetOrdinals()
    {
        var json = """
        {
            "$defs": {
                "addr": {
                    "type": "object",
                    "properties": {
                        "city": { "type": "string" }
                    }
                }
            }
        }
        """;
        var bytes = Encoding.UTF8.GetBytes(json);
        var root = JsonSchemaLoader.Load(bytes);

        var (nameToOrdinal, count) = SchemaIndexer.AssignOrdinals(root!);

        nameToOrdinal.Should().ContainKey("/addr/city");
        count.Should().Be(1);
    }

    // ── JsonSchemaLoader: keyword coverage ───────────────────────────

    [Test]
    public void Load_AllNumericKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "number",
            "minimum": 0,
            "maximum": 100,
            "exclusiveMinimum": -1,
            "exclusiveMaximum": 101,
            "multipleOf": 5
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.Minimum.Should().Be(0);
        schema.Root.Maximum.Should().Be(100);
        schema.Root.ExclusiveMinimum.Should().Be(-1);
        schema.Root.ExclusiveMaximum.Should().Be(101);
        schema.Root.MultipleOf.Should().Be(5);
    }

    [Test]
    public void Load_AllStringKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "string",
            "minLength": 1,
            "maxLength": 50,
            "pattern": "^[a-z]+$",
            "format": "email"
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.MinLength.Should().Be(1);
        schema.Root.MaxLength.Should().Be(50);
        schema.Root.Pattern.Should().NotBeNull();
        schema.Root.Format.Should().Be("email");
    }

    [Test]
    public void Load_AllArrayKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "array",
            "items": { "type": "string" },
            "minItems": 1,
            "maxItems": 10,
            "uniqueItems": true,
            "contains": { "type": "number" },
            "minContains": 1,
            "maxContains": 3,
            "prefixItems": [{ "type": "string" }]
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.MinItems.Should().Be(1);
        schema.Root.MaxItems.Should().Be(10);
        schema.Root.UniqueItems.Should().BeTrue();
        schema.Root.Items.Should().NotBeNull();
        schema.Root.Contains.Should().NotBeNull();
        schema.Root.MinContains.Should().Be(1);
        schema.Root.MaxContains.Should().Be(3);
        schema.Root.PrefixItems.Should().HaveCount(1);
    }

    [Test]
    public void Load_AllObjectKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "type": "object",
            "properties": { "a": { "type": "string" } },
            "required": ["a"],
            "minProperties": 1,
            "maxProperties": 5,
            "additionalProperties": { "type": "number" },
            "patternProperties": { "^x": { "type": "boolean" } },
            "propertyNames": { "maxLength": 10 },
            "dependentRequired": { "a": ["b"] },
            "dependentSchemas": { "a": { "required": ["c"] } }
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.MinProperties.Should().Be(1);
        schema.Root.MaxProperties.Should().Be(5);
        schema.Root.Required.Should().Contain("a");
        schema.Root.AdditionalProperties.Should().NotBeNull();
        schema.Root.PatternProperties.Should().ContainKey("^x");
        schema.Root.PropertyNames.Should().NotBeNull();
        schema.Root.DependentRequired.Should().ContainKey("a");
        schema.Root.DependentSchemas.Should().ContainKey("a");
    }

    [Test]
    public void Load_CompositionKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "allOf": [{ "type": "object" }],
            "anyOf": [{ "type": "string" }, { "type": "number" }],
            "oneOf": [{ "minimum": 0 }],
            "not": { "type": "null" }
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.AllOf.Should().HaveCount(1);
        schema.Root.AnyOf.Should().HaveCount(2);
        schema.Root.OneOf.Should().HaveCount(1);
        schema.Root.Not.Should().NotBeNull();
    }

    [Test]
    public void Load_ConditionalKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "if": { "type": "number" },
            "then": { "minimum": 0 },
            "else": { "type": "string" }
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.If.Should().NotBeNull();
        schema.Root.Then.Should().NotBeNull();
        schema.Root.Else.Should().NotBeNull();
    }

    [Test]
    public void Load_MetadataKeywords()
    {
        var schema = JsonContractSchema.Load("""
        {
            "$id": "https://example.com/schema",
            "$anchor": "root",
            "$comment": "test schema",
            "description": "A test",
            "title": "Test"
        }
        """);

        schema.Should().NotBeNull();
        schema!.Root.Id.Should().Be("https://example.com/schema");
        schema.Root.Anchor.Should().Be("root");
    }

    [Test]
    public void Load_BooleanSchemaTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("true");
        var schema = JsonContractSchema.Load(bytes);

        schema.Should().NotBeNull();
        schema!.Root.BooleanSchema.Should().Be(true);
    }

    [Test]
    public void Load_BooleanSchemaFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("false");
        var schema = JsonContractSchema.Load(bytes);

        schema.Should().NotBeNull();
        schema!.Root.BooleanSchema.Should().Be(false);
    }

    [Test]
    public void Load_MultipleTypes()
    {
        var schema = JsonContractSchema.Load("""{"type":["string","number"]}""");

        schema.Should().NotBeNull();
    }

    [Test]
    public void Load_EnumKeyword()
    {
        var schema = JsonContractSchema.Load("""{"enum":["red","green","blue"]}""");

        schema.Should().NotBeNull();
        schema!.Root.Enum.Should().NotBeNull();
        schema.Root.Enum!.Length.Should().Be(3);
    }

    [Test]
    public void Load_ConstKeyword()
    {
        var schema = JsonContractSchema.Load("""{"const":"fixed"}""");

        schema.Should().NotBeNull();
        schema!.Root.Const.Should().NotBeNull();
    }
}
