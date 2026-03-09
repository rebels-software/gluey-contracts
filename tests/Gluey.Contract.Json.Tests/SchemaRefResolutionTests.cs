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
}
