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

namespace Gluey.Contract.Json.Tests;

[TestFixture]
[Category("SchemaRef")]
public class SchemaRefResolutionTests
{
    // ── SCHM-03a: $ref to $defs resolves ──────────────────────────────────

    [Test]
    public void Ref_To_Defs_Resolves()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "billing": { "$ref": "#/$defs/address" }
            },
            "$defs": {
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

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        // billing + street + city + address (from $defs) = 4 properties
        schema!.PropertyCount.Should().BeGreaterThanOrEqualTo(3);

        // Verify ResolvedRef is set on the billing node
        var billingNode = schema.Root.Properties!["billing"];
        billingNode.Ref.Should().Be("#/$defs/address");
        billingNode.ResolvedRef.Should().NotBeNull();
        billingNode.ResolvedRef!.Properties.Should().ContainKey("street");
        billingNode.ResolvedRef!.Properties.Should().ContainKey("city");
    }

    // ── SCHM-03b: Direct self-reference cycle detected ────────────────────

    [Test]
    public void Direct_Cycle_Detected()
    {
        // A $defs entry that $refs itself
        var json = """
        {
            "type": "object",
            "$defs": {
                "loop": { "$ref": "#/$defs/loop" }
            },
            "properties": {
                "x": { "$ref": "#/$defs/loop" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── SCHM-03c: Mutual cycle (A refs B, B refs A) ───────────────────────

    [Test]
    public void Mutual_Cycle_Detected()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "a": { "$ref": "#/$defs/b" },
                "b": { "$ref": "#/$defs/a" }
            },
            "properties": {
                "x": { "$ref": "#/$defs/a" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── SCHM-03d: Transitive cycle (A refs B, B refs C, C refs A) ─────────

    [Test]
    public void Transitive_Cycle_Detected()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "a": { "$ref": "#/$defs/b" },
                "b": { "$ref": "#/$defs/c" },
                "c": { "$ref": "#/$defs/a" }
            },
            "properties": {
                "x": { "$ref": "#/$defs/a" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── SCHM-03e: Unresolvable $ref fails ─────────────────────────────────

    [Test]
    public void Unresolvable_Ref_Fails()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "x": { "$ref": "#/$defs/nonexistent" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── SCHM-03f: Multiple refs to same $defs entry succeed ───────────────

    [Test]
    public void Multiple_Refs_Same_Target()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "billing": { "$ref": "#/$defs/address" },
                "shipping": { "$ref": "#/$defs/address" }
            },
            "$defs": {
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" }
                    }
                }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();

        // Both should resolve to the same target
        var billing = schema!.Root.Properties!["billing"];
        var shipping = schema.Root.Properties!["shipping"];
        billing.ResolvedRef.Should().NotBeNull();
        shipping.ResolvedRef.Should().NotBeNull();
        billing.ResolvedRef.Should().BeSameAs(shipping.ResolvedRef);
    }

    // ── SCHM-04a: $anchor resolution ──────────────────────────────────────

    [Test]
    public void Anchor_Resolution()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "addr": {
                    "$anchor": "my-anchor",
                    "type": "object",
                    "properties": {
                        "zip": { "type": "string" }
                    }
                }
            },
            "properties": {
                "location": { "$ref": "#my-anchor" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();

        var location = schema!.Root.Properties!["location"];
        location.ResolvedRef.Should().NotBeNull();
        location.ResolvedRef!.Anchor.Should().Be("my-anchor");
        location.ResolvedRef!.Properties.Should().ContainKey("zip");
    }

    // ── SCHM-04b: Duplicate $anchor fails ─────────────────────────────────

    [Test]
    public void Duplicate_Anchor_Fails()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "a": { "$anchor": "dup" },
                "b": { "$anchor": "dup" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── SCHM-06b: Cross-schema $ref resolves via registry ─────────────────

    [Test]
    public void Cross_Schema_Ref_Resolves()
    {
        // First, load the referenced schema and register it
        var otherJson = """
        {
            "type": "object",
            "$defs": {
                "foo": {
                    "type": "object",
                    "properties": {
                        "bar": { "type": "string" }
                    }
                }
            }
        }
        """;

        var otherResult = JsonContractSchema.TryLoad(otherJson, out var otherSchema);
        otherResult.Should().BeTrue();

        var registry = new SchemaRegistry();
        registry.Add("https://example.com/other", otherSchema!.Root);

        // Now load a schema that references it
        var json = """
        {
            "type": "object",
            "properties": {
                "ext": { "$ref": "https://example.com/other#/$defs/foo" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeTrue();
        schema.Should().NotBeNull();

        var ext = schema!.Root.Properties!["ext"];
        ext.ResolvedRef.Should().NotBeNull();
        ext.ResolvedRef!.Properties.Should().ContainKey("bar");
    }

    // ── SCHM-06c: Cross-schema $ref to unregistered URI fails ─────────────

    [Test]
    public void Cross_Schema_Unregistered_Fails()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "ext": { "$ref": "https://example.com/missing#/$defs/foo" }
            }
        }
        """;

        var registry = new SchemaRegistry();
        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── Empty fragment $ref "#" resolves to root ──────────────────────────

    [Test]
    public void Empty_Fragment_Ref_Resolves_To_Root()
    {
        // A $defs entry that refs root is non-cyclic when used from a property
        var json = """
        {
            "type": "object",
            "$defs": {
                "self": { "$ref": "#" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();

        var selfDef = schema!.Root.Defs!["self"];
        selfDef.ResolvedRef.Should().NotBeNull();
        selfDef.ResolvedRef.Should().BeSameAs(schema.Root);
    }

    // ── $ref with sibling keywords ────────────────────────────────────────

    [Test]
    public void Ref_With_Sibling_Keywords()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "base": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" }
                    }
                }
            },
            "properties": {
                "person": {
                    "$ref": "#/$defs/base",
                    "description": "A person with a name"
                }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();

        var person = schema!.Root.Properties!["person"];
        person.Ref.Should().Be("#/$defs/base");
        person.Description.Should().Be("A person with a name");
        person.ResolvedRef.Should().NotBeNull();
        person.ResolvedRef!.Properties.Should().ContainKey("name");
    }

    // ── Existing tests still pass (regression guard) ──────────────────────

    [Test]
    public void Schema_Without_Refs_Still_Loads()
    {
        // Plain schema with no $ref should still load fine
        var json = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(2);
    }

    // ── Cross-schema $ref without fragment ────────────────────────────────

    [Test]
    public void Cross_Schema_Ref_No_Fragment_Resolves()
    {
        var otherJson = """{"type":"string"}""";
        var otherResult = JsonContractSchema.TryLoad(otherJson, out var otherSchema);
        otherResult.Should().BeTrue();

        var registry = new SchemaRegistry();
        registry.Add("https://example.com/string-type", otherSchema!.Root);

        var json = """
        {
            "type": "object",
            "properties": {
                "name": { "$ref": "https://example.com/string-type" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        var name = schema!.Root.Properties!["name"];
        name.ResolvedRef.Should().NotBeNull();
    }

    // ── Cross-schema $ref with # only ────────────────────────────────────

    [Test]
    public void Cross_Schema_Ref_EmptyFragment_Resolves()
    {
        var otherJson = """{"type":"string"}""";
        var otherResult = JsonContractSchema.TryLoad(otherJson, out var otherSchema);
        otherResult.Should().BeTrue();

        var registry = new SchemaRegistry();
        registry.Add("https://example.com/other", otherSchema!.Root);

        var json = """
        {
            "type": "object",
            "properties": {
                "val": { "$ref": "https://example.com/other#" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeTrue();
        var val = schema!.Root.Properties!["val"];
        val.ResolvedRef.Should().NotBeNull();
    }

    // ── Cross-schema $ref without registry fails ─────────────────────────

    [Test]
    public void Cross_Schema_Ref_No_Registry_Fails()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "ext": { "$ref": "https://example.com/missing" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── Cross-schema $ref with anchor fragment unsupported ────────────────

    [Test]
    public void Cross_Schema_Ref_Anchor_Fragment_Fails()
    {
        var otherJson = """{"$anchor":"foo","type":"string"}""";
        var otherResult = JsonContractSchema.TryLoad(otherJson, out var otherSchema);
        otherResult.Should().BeTrue();

        var registry = new SchemaRegistry();
        registry.Add("https://example.com/other", otherSchema!.Root);

        var json = """
        {
            "type": "object",
            "properties": {
                "val": { "$ref": "https://example.com/other#foo" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeFalse();
    }

    // ── JSON Pointer to various schema keywords ──────────────────────────

    [Test]
    public void Ref_To_Items_Resolves()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "arr": { "type": "array", "items": { "type": "string" } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/arr/items" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        var x = schema!.Root.Properties!["x"];
        x.ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_AdditionalProperties_Resolves()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "obj": { "type": "object", "additionalProperties": { "type": "number" } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/additionalProperties" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        var x = schema!.Root.Properties!["x"];
        x.ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_Not_Resolves()
    {
        var json = """
        {
            "type": "object",
            "$defs": {
                "neg": { "not": { "type": "string" } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/neg/not" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        var x = schema!.Root.Properties!["x"];
        x.ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_IfThenElse_Resolves()
    {
        var json = """
        {
            "$defs": {
                "cond": {
                    "if": { "type": "number" },
                    "then": { "minimum": 0 },
                    "else": { "type": "string" }
                }
            },
            "properties": {
                "a": { "$ref": "#/$defs/cond/if" },
                "b": { "$ref": "#/$defs/cond/then" },
                "c": { "$ref": "#/$defs/cond/else" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["a"].ResolvedRef.Should().NotBeNull();
        schema.Root.Properties!["b"].ResolvedRef.Should().NotBeNull();
        schema.Root.Properties!["c"].ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_Contains_Resolves()
    {
        var json = """
        {
            "$defs": {
                "arr": { "type": "array", "contains": { "type": "number" } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/arr/contains" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_PropertyNames_Resolves()
    {
        var json = """
        {
            "$defs": {
                "obj": { "type": "object", "propertyNames": { "maxLength": 5 } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/propertyNames" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── JSON Pointer indexed (allOf/anyOf/oneOf/prefixItems) ─────────────

    [Test]
    public void Ref_To_AllOf_Index_Resolves()
    {
        var json = """
        {
            "$defs": {
                "comp": { "allOf": [{"type":"string"}, {"minLength":1}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/comp/0" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    [Test]
    public void Ref_To_PrefixItems_Index_Resolves()
    {
        var json = """
        {
            "$defs": {
                "arr": { "type": "array", "prefixItems": [{"type":"string"}, {"type":"number"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/arr/1" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Container keyword without key segment ────────────────────────────

    [Test]
    public void Ref_To_Container_Without_Key_Fails()
    {
        var json = """
        {
            "$defs": {
                "obj": { "type": "object", "properties": { "a": {"type":"string"} } }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/properties" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Invalid index in JSON Pointer ─────────────────────────────────────

    [Test]
    public void Ref_To_Invalid_Index_Fails()
    {
        var json = """
        {
            "$defs": {
                "comp": { "allOf": [{"type":"string"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/comp/99" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Ref to patternProperties via container ───────────────────────────

    [Test]
    public void Ref_To_PatternProperties_Entry_Resolves()
    {
        var json = """
        {
            "$defs": {
                "obj": {
                    "type": "object",
                    "patternProperties": { "^s": { "type": "string" } }
                }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/patternProperties/^s" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Ref to dependentSchemas entry ─────────────────────────────────────

    [Test]
    public void Ref_To_DependentSchemas_Entry_Resolves()
    {
        var json = """
        {
            "$defs": {
                "obj": {
                    "type": "object",
                    "dependentSchemas": { "a": { "required": ["b"] } }
                }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/dependentSchemas/a" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── RFC 6901 escaping (~0, ~1) ───────────────────────────────────────

    [Test]
    public void Ref_With_Tilde_Escaped_Segment()
    {
        var json = """
        {
            "$defs": {
                "a~b": { "type": "string" }
            },
            "properties": {
                "x": { "$ref": "#/$defs/a~0b" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Ref to properties container entry ────────────────────────────────

    [Test]
    public void Ref_To_Properties_Entry_Resolves()
    {
        var json = """
        {
            "$defs": {
                "obj": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" }
                    }
                }
            },
            "properties": {
                "x": { "$ref": "#/$defs/obj/properties/name" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Ref to anyOf indexed entry ───────────────────────────────────────

    [Test]
    public void Ref_To_AnyOf_Index_Resolves()
    {
        var json = """
        {
            "$defs": {
                "comp": { "anyOf": [{"type":"string"}, {"type":"number"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/comp/0" },
                "y": { "$ref": "#/$defs/comp/1" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
        schema.Root.Properties!["y"].ResolvedRef.Should().NotBeNull();
    }

    // ── Ref to oneOf indexed entry ───────────────────────────────────────

    [Test]
    public void Ref_To_OneOf_Index_Resolves()
    {
        var json = """
        {
            "$defs": {
                "comp": { "oneOf": [{"type":"string"}, {"type":"integer"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/comp/0" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Unresolvable anchor ──────────────────────────────────────────────

    [Test]
    public void Unresolvable_Anchor_Fails()
    {
        var json = """
        {
            "properties": {
                "x": { "$ref": "#nonexistent-anchor" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── Ref inside composition keywords triggers WalkChildren failure ────

    [Test]
    public void Ref_Inside_AllOf_Unresolvable_Fails()
    {
        var json = """
        {
            "allOf": [
                { "$ref": "#/$defs/missing" }
            ]
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_AnyOf_Unresolvable_Fails()
    {
        var json = """
        {
            "anyOf": [
                { "$ref": "#/$defs/missing" }
            ]
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_OneOf_Unresolvable_Fails()
    {
        var json = """
        {
            "oneOf": [
                { "$ref": "#/$defs/missing" }
            ]
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_Not_Unresolvable_Fails()
    {
        var json = """
        {
            "not": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Ref inside conditional keywords ──────────────────────────────────

    [Test]
    public void Ref_Inside_If_Unresolvable_Fails()
    {
        var json = """
        {
            "if": { "$ref": "#/$defs/missing" },
            "then": { "type": "string" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_Then_Unresolvable_Fails()
    {
        var json = """
        {
            "if": { "type": "number" },
            "then": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_Else_Unresolvable_Fails()
    {
        var json = """
        {
            "if": { "type": "number" },
            "else": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Ref inside array applicators ─────────────────────────────────────

    [Test]
    public void Ref_Inside_Items_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "array",
            "items": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_PrefixItems_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "array",
            "prefixItems": [{ "$ref": "#/$defs/missing" }]
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_Contains_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "array",
            "contains": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Ref inside object applicators ────────────────────────────────────

    [Test]
    public void Ref_Inside_AdditionalProperties_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "object",
            "additionalProperties": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_PatternProperties_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "object",
            "patternProperties": {
                "^s": { "$ref": "#/$defs/missing" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_DependentSchemas_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "object",
            "dependentSchemas": {
                "a": { "$ref": "#/$defs/missing" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    [Test]
    public void Ref_Inside_PropertyNames_Unresolvable_Fails()
    {
        var json = """
        {
            "type": "object",
            "propertyNames": { "$ref": "#/$defs/missing" }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Duplicate anchor inside nested defs ──────────────────────────────

    [Test]
    public void Duplicate_Anchor_In_Nested_Properties_Fails()
    {
        var json = """
        {
            "type": "object",
            "properties": {
                "a": { "$anchor": "dup" },
                "b": { "$anchor": "dup" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Negative index in pointer ────────────────────────────────────────

    [Test]
    public void Ref_With_Negative_Index_Fails()
    {
        var json = """
        {
            "$defs": {
                "arr": { "allOf": [{"type":"string"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/arr/-1" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Non-numeric segment for indexed navigation ───────────────────────

    [Test]
    public void Ref_With_NonNumeric_Index_Fails()
    {
        var json = """
        {
            "$defs": {
                "arr": { "allOf": [{"type":"string"}] }
            },
            "properties": {
                "x": { "$ref": "#/$defs/arr/abc" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeFalse();
    }

    // ── Ref chain: A -> B -> C (non-cyclic) ──────────────────────────────

    [Test]
    public void Ref_Chain_NonCyclic_Resolves()
    {
        var json = """
        {
            "$defs": {
                "a": { "$ref": "#/$defs/b" },
                "b": { "$ref": "#/$defs/c" },
                "c": { "type": "string" }
            },
            "properties": {
                "x": { "$ref": "#/$defs/a" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }

    // ── Cross-schema ref to non-existent base URI ────────────────────────

    [Test]
    public void Cross_Schema_Ref_NonExistent_BaseUri_With_Fragment_Fails()
    {
        var registry = new SchemaRegistry();
        var json = """
        {
            "properties": {
                "x": { "$ref": "https://example.com/nonexistent#/$defs/foo" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeFalse();
    }

    // ── Cross-schema ref with JSON pointer fragment to valid path ────────

    [Test]
    public void Cross_Schema_Ref_With_Pointer_To_Nested_Property()
    {
        var otherJson = """
        {
            "type": "object",
            "properties": {
                "nested": {
                    "type": "object",
                    "properties": {
                        "deep": { "type": "number" }
                    }
                }
            }
        }
        """;
        var otherResult = JsonContractSchema.TryLoad(otherJson, out var otherSchema);
        otherResult.Should().BeTrue();

        var registry = new SchemaRegistry();
        registry.Add("https://example.com/other", otherSchema!.Root);

        var json = """
        {
            "properties": {
                "x": { "$ref": "https://example.com/other#/properties/nested" }
            }
        }
        """;

        var result = JsonContractSchema.TryLoad(json, out var schema, registry);

        result.Should().BeTrue();
        schema!.Root.Properties!["x"].ResolvedRef.Should().NotBeNull();
    }
}
