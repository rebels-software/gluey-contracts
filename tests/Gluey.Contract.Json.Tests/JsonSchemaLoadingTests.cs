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
}
