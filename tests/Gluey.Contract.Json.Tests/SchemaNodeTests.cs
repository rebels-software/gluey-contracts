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
public class SchemaNodeTests
{
    private static SchemaNode? LoadNode(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return JsonSchemaLoader.Load(bytes);
    }

    // ── Basic loading ─────────────────────────────────────────────────

    [Test]
    public void Load_SimpleObjectType_ProducesRootNode()
    {
        var node = LoadNode("""{"type":"object"}""");

        node.Should().NotBeNull();
        node!.Type.Should().Be(SchemaType.Object);
        node.Path.Should().BeEmpty();
    }

    [Test]
    public void Load_EmptyObject_ProducesValidNode()
    {
        var node = LoadNode("{}");

        node.Should().NotBeNull();
        node!.Type.Should().BeNull();
        node.Path.Should().BeEmpty();
    }

    // ── Properties and paths ──────────────────────────────────────────

    [Test]
    public void Load_Properties_ProducesChildNodes()
    {
        var node = LoadNode("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "address": { "type": "object" }
            }
        }
        """);

        node.Should().NotBeNull();
        node!.Properties.Should().NotBeNull();
        node.Properties.Should().ContainKey("name");
        node.Properties.Should().ContainKey("address");
        node.Properties!["name"].Path.Should().Be("/name");
        node.Properties["address"].Path.Should().Be("/address");
    }

    [Test]
    public void Load_NestedProperties_ProducesCorrectPaths()
    {
        var node = LoadNode("""
        {
            "type": "object",
            "properties": {
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

        var address = node!.Properties!["address"];
        address.Properties!["street"].Path.Should().Be("/address/street");
        address.Properties["city"].Path.Should().Be("/address/city");
    }

    [Test]
    public void Load_PathEscaping_TildeEscaped()
    {
        var node = LoadNode("""
        {
            "properties": {
                "a~b": { "type": "string" }
            }
        }
        """);

        node!.Properties!["a~b"].Path.Should().Be("/a~0b");
    }

    [Test]
    public void Load_PathEscaping_SlashEscaped()
    {
        var node = LoadNode("""
        {
            "properties": {
                "a/b": { "type": "string" }
            }
        }
        """);

        node!.Properties!["a/b"].Path.Should().Be("/a~1b");
    }

    // ── Boolean schemas ───────────────────────────────────────────────

    [Test]
    public void Load_BooleanSchemaTrue_ReturnsTrueSentinel()
    {
        var bytes = Encoding.UTF8.GetBytes("true");
        var node = JsonSchemaLoader.Load(bytes);

        node.Should().BeSameAs(SchemaNode.True);
        node!.BooleanSchema.Should().BeTrue();
    }

    [Test]
    public void Load_BooleanSchemaFalse_ReturnsFalseSentinel()
    {
        var bytes = Encoding.UTF8.GetBytes("false");
        var node = JsonSchemaLoader.Load(bytes);

        node.Should().BeSameAs(SchemaNode.False);
        node!.BooleanSchema.Should().BeFalse();
    }

    [Test]
    public void Load_AdditionalPropertiesFalse_ReturnsFalseSentinel()
    {
        var node = LoadNode("""
        {
            "type": "object",
            "additionalProperties": false
        }
        """);

        node!.AdditionalProperties.Should().BeSameAs(SchemaNode.False);
    }

    // ── Type keyword ──────────────────────────────────────────────────

    [Test]
    public void Load_TypeArray_ProducesCombinedFlags()
    {
        var node = LoadNode("""{"type": ["string", "null"]}""");

        node!.Type.Should().Be(SchemaType.String | SchemaType.Null);
    }

    // ── Required ──────────────────────────────────────────────────────

    [Test]
    public void Load_Required_PopulatesStringArray()
    {
        var node = LoadNode("""{"required": ["name", "email"]}""");

        node!.Required.Should().NotBeNull();
        node.Required.Should().BeEquivalentTo(new[] { "name", "email" });
    }

    // ── Composition keywords ──────────────────────────────────────────

    [Test]
    public void Load_AllOf_ProducesSchemaNodeArray()
    {
        var node = LoadNode("""
        {
            "allOf": [
                { "type": "object" },
                { "required": ["name"] }
            ]
        }
        """);

        node!.AllOf.Should().NotBeNull();
        node.AllOf.Should().HaveCount(2);
        node.AllOf![0].Type.Should().Be(SchemaType.Object);
        node.AllOf[1].Required.Should().Contain("name");
    }

    [Test]
    public void Load_AnyOf_ProducesSchemaNodeArray()
    {
        var node = LoadNode("""
        {
            "anyOf": [
                { "type": "string" },
                { "type": "integer" }
            ]
        }
        """);

        node!.AnyOf.Should().HaveCount(2);
    }

    [Test]
    public void Load_OneOf_ProducesSchemaNodeArray()
    {
        var node = LoadNode("""
        {
            "oneOf": [
                { "type": "string" },
                { "type": "number" }
            ]
        }
        """);

        node!.OneOf.Should().HaveCount(2);
    }

    // ── Items ─────────────────────────────────────────────────────────

    [Test]
    public void Load_Items_ProducesItemsChild()
    {
        var node = LoadNode("""
        {
            "type": "array",
            "items": { "type": "string" }
        }
        """);

        node!.Items.Should().NotBeNull();
        node.Items!.Type.Should().Be(SchemaType.String);
    }

    // ── PrefixItems ───────────────────────────────────────────────────

    [Test]
    public void Load_PrefixItems_ProducesSchemaNodeArray()
    {
        var node = LoadNode("""
        {
            "prefixItems": [
                { "type": "string" },
                { "type": "integer" }
            ]
        }
        """);

        node!.PrefixItems.Should().HaveCount(2);
    }

    // ── $ref ──────────────────────────────────────────────────────────

    [Test]
    public void Load_Ref_StoredOnRefField()
    {
        var node = LoadNode("""{"$ref": "#/$defs/Address"}""");

        node!.Ref.Should().Be("#/$defs/Address");
    }

    // ── $defs ─────────────────────────────────────────────────────────

    [Test]
    public void Load_Defs_ProducesSchemaNodeMap()
    {
        var node = LoadNode("""
        {
            "$defs": {
                "Address": { "type": "object" }
            }
        }
        """);

        node!.Defs.Should().ContainKey("Address");
        node.Defs!["Address"].Type.Should().Be(SchemaType.Object);
    }

    // ── Numeric keywords ──────────────────────────────────────────────

    [Test]
    public void Load_NumericKeywords_StoredAsDecimal()
    {
        var node = LoadNode("""
        {
            "minimum": 0,
            "maximum": 100,
            "exclusiveMinimum": -1,
            "exclusiveMaximum": 101,
            "multipleOf": 5
        }
        """);

        node!.Minimum.Should().Be(0m);
        node.Maximum.Should().Be(100m);
        node.ExclusiveMinimum.Should().Be(-1m);
        node.ExclusiveMaximum.Should().Be(101m);
        node.MultipleOf.Should().Be(5m);
    }

    // ── String keywords ───────────────────────────────────────────────

    [Test]
    public void Load_StringKeywords_StoredCorrectly()
    {
        var node = LoadNode("""
        {
            "minLength": 1,
            "maxLength": 255,
            "pattern": "^[a-z]+$"
        }
        """);

        node!.MinLength.Should().Be(1);
        node.MaxLength.Should().Be(255);
        node.Pattern.Should().Be("^[a-z]+$");
    }

    // ── Unknown keywords ──────────────────────────────────────────────

    [Test]
    public void Load_UnknownKeywords_SilentlyIgnored()
    {
        var node = LoadNode("""
        {
            "type": "string",
            "x-custom": 42,
            "x-vendor-info": { "nested": true }
        }
        """);

        node.Should().NotBeNull();
        node!.Type.Should().Be(SchemaType.String);
    }

    // ── Invalid input ─────────────────────────────────────────────────

    [Test]
    public void Load_InvalidJson_NotObject_ReturnsNull()
    {
        LoadNode("[1,2,3]").Should().BeNull();
    }

    [Test]
    public void Load_InvalidJson_NullLiteral_ReturnsNull()
    {
        var bytes = Encoding.UTF8.GetBytes("null");
        JsonSchemaLoader.Load(bytes).Should().BeNull();
    }

    // ── Enum ──────────────────────────────────────────────────────────

    [Test]
    public void Load_Enum_StoredAsRawUtf8ByteArrays()
    {
        var node = LoadNode("""{"enum": ["a", "b", 1]}""");

        node!.Enum.Should().NotBeNull();
        node.Enum.Should().HaveCount(3);
    }

    // ── Const ─────────────────────────────────────────────────────────

    [Test]
    public void Load_Const_StoredAsRawUtf8ByteArray()
    {
        var node = LoadNode("""{"const": "fixed"}""");

        node!.Const.Should().NotBeNull();
        // The serialized value should contain "fixed" in UTF-8
        Encoding.UTF8.GetString(node.Const!).Should().Contain("fixed");
    }

    // ── Conditional keywords ──────────────────────────────────────────

    [Test]
    public void Load_IfThenElse_ProducesSchemaNodes()
    {
        var node = LoadNode("""
        {
            "if": { "type": "string" },
            "then": { "minLength": 1 },
            "else": { "type": "number" }
        }
        """);

        node!.If.Should().NotBeNull();
        node.If!.Type.Should().Be(SchemaType.String);
        node.Then.Should().NotBeNull();
        node.Then!.MinLength.Should().Be(1);
        node.Else.Should().NotBeNull();
        node.Else!.Type.Should().Be(SchemaType.Number);
    }

    // ── Not keyword ───────────────────────────────────────────────────

    [Test]
    public void Load_Not_ProducesSchemaNode()
    {
        var node = LoadNode("""
        {
            "not": { "type": "string" }
        }
        """);

        node!.Not.Should().NotBeNull();
        node.Not!.Type.Should().Be(SchemaType.String);
    }

    // ── Contains keyword ──────────────────────────────────────────────

    [Test]
    public void Load_Contains_ProducesSchemaNode()
    {
        var node = LoadNode("""
        {
            "contains": { "type": "integer" }
        }
        """);

        node!.Contains.Should().NotBeNull();
        node.Contains!.Type.Should().Be(SchemaType.Integer);
    }

    // ── DependentRequired ─────────────────────────────────────────────

    [Test]
    public void Load_DependentRequired_ProducesMap()
    {
        var node = LoadNode("""
        {
            "dependentRequired": {
                "name": ["first", "last"]
            }
        }
        """);

        node!.DependentRequired.Should().ContainKey("name");
        node.DependentRequired!["name"].Should().BeEquivalentTo(new[] { "first", "last" });
    }

    // ── DependentSchemas ──────────────────────────────────────────────

    [Test]
    public void Load_DependentSchemas_ProducesSchemaNodeMap()
    {
        var node = LoadNode("""
        {
            "dependentSchemas": {
                "name": { "required": ["first"] }
            }
        }
        """);

        node!.DependentSchemas.Should().ContainKey("name");
        node.DependentSchemas!["name"].Required.Should().Contain("first");
    }

    // ── PatternProperties ─────────────────────────────────────────────

    [Test]
    public void Load_PatternProperties_ProducesSchemaNodeMap()
    {
        var node = LoadNode("""
        {
            "patternProperties": {
                "^x-": { "type": "string" }
            }
        }
        """);

        node!.PatternProperties.Should().ContainKey("^x-");
        node.PatternProperties!["^x-"].Type.Should().Be(SchemaType.String);
    }

    // ── PropertyNames ─────────────────────────────────────────────────

    [Test]
    public void Load_PropertyNames_ProducesSchemaNode()
    {
        var node = LoadNode("""
        {
            "propertyNames": { "minLength": 1 }
        }
        """);

        node!.PropertyNames.Should().NotBeNull();
        node.PropertyNames!.MinLength.Should().Be(1);
    }

    // ── Array keywords ────────────────────────────────────────────────

    [Test]
    public void Load_ArrayKeywords_StoredCorrectly()
    {
        var node = LoadNode("""
        {
            "minItems": 1,
            "maxItems": 10,
            "uniqueItems": true,
            "minContains": 1,
            "maxContains": 5
        }
        """);

        node!.MinItems.Should().Be(1);
        node.MaxItems.Should().Be(10);
        node.UniqueItems.Should().BeTrue();
        node.MinContains.Should().Be(1);
        node.MaxContains.Should().Be(5);
    }

    // ── Object count keywords ─────────────────────────────────────────

    [Test]
    public void Load_ObjectCountKeywords_StoredCorrectly()
    {
        var node = LoadNode("""
        {
            "minProperties": 1,
            "maxProperties": 10
        }
        """);

        node!.MinProperties.Should().Be(1);
        node.MaxProperties.Should().Be(10);
    }

    // ── Meta-data / annotations ───────────────────────────────────────

    [Test]
    public void Load_Annotations_StoredCorrectly()
    {
        var node = LoadNode("""
        {
            "title": "My Schema",
            "description": "A test schema",
            "format": "email",
            "deprecated": true
        }
        """);

        node!.Title.Should().Be("My Schema");
        node.Description.Should().Be("A test schema");
        node.Format.Should().Be("email");
        node.Deprecated.Should().BeTrue();
    }

    // ── Identity keywords ─────────────────────────────────────────────

    [Test]
    public void Load_IdentityKeywords_StoredCorrectly()
    {
        var node = LoadNode("""
        {
            "$id": "https://example.com/schema",
            "$anchor": "myAnchor",
            "$comment": "A comment",
            "$dynamicRef": "#myDynamic",
            "$dynamicAnchor": "myDynamic"
        }
        """);

        node!.Id.Should().Be("https://example.com/schema");
        node.Anchor.Should().Be("myAnchor");
        node.Comment.Should().Be("A comment");
        node.DynamicRef.Should().Be("#myDynamic");
        node.DynamicAnchor.Should().Be("myDynamic");
    }
}
