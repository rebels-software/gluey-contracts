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
public class NestedPropertyAccessTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static JsonContractSchema LoadSchema(string json)
        => JsonContractSchema.Load(json)!;

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Nested object access ────────────────────────────────────────────

    [Test]
    public void NestedProperty_Street_ReturnsCorrectValue()
    {
        var schema = LoadSchema("""
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
        var data = Utf8("""{"address":{"street":"123 Main St","city":"Springfield"}}""");

        schema.TryParse(data, out var result);

        result["/address"]["street"].HasValue.Should().BeTrue();
        result["/address"]["street"].GetString().Should().Be("123 Main St");
        result.Dispose();
    }

    [Test]
    public void NestedProperty_City_ReturnsCorrectValue()
    {
        var schema = LoadSchema("""
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
        var data = Utf8("""{"address":{"street":"123 Main St","city":"Springfield"}}""");

        schema.TryParse(data, out var result);

        result["/address"]["city"].HasValue.Should().BeTrue();
        result["/address"]["city"].GetString().Should().Be("Springfield");
        result.Dispose();
    }

    [Test]
    public void NestedProperty_MissingChild_ReturnsEmpty()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" }
                    }
                }
            }
        }
        """);
        var data = Utf8("""{"address":{"street":"123 Main St"}}""");

        schema.TryParse(data, out var result);

        result["/address"]["missing"].HasValue.Should().BeFalse();
        result.Dispose();
    }

    [Test]
    public void NestedProperty_StringIndexerOnNonObject_ReturnsEmpty()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" }
            }
        }
        """);
        var data = Utf8("""{"name":"Alice"}""");

        schema.TryParse(data, out var result);

        // "name" is a string, not an object -- string indexer should return Empty
        result["/name"]["anything"].HasValue.Should().BeFalse();
        result.Dispose();
    }

    // ── Slash-prefix normalization ──────────────────────────────────────

    [Test]
    public void TopLevel_WithoutSlashPrefix_ReturnsValue()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" }
            }
        }
        """);
        var data = Utf8("""{"name":"Alice"}""");

        schema.TryParse(data, out var result);

        result["name"].HasValue.Should().BeTrue();
        result["name"].GetString().Should().Be("Alice");
        result.Dispose();
    }

    [Test]
    public void Nested_WithoutSlashPrefix_ChainsCorrectly()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" }
                    }
                }
            }
        }
        """);
        var data = Utf8("""{"address":{"street":"Main"}}""");

        schema.TryParse(data, out var result);

        result["address"]["street"].HasValue.Should().BeTrue();
        result["address"]["street"].GetString().Should().Be("Main");
        result.Dispose();
    }

    [Test]
    public void TopLevel_WithSlashPrefix_StillWorks()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" }
            }
        }
        """);
        var data = Utf8("""{"name":"Alice"}""");

        schema.TryParse(data, out var result);

        result["/name"].HasValue.Should().BeTrue();
        result["/name"].GetString().Should().Be("Alice");
        result.Dispose();
    }

    // ── Deep nesting ─────────────────────────────────────────────────────

    [Test]
    public void NestedProperty_DeepNesting_ThreeLevels()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "a": {
                    "type": "object",
                    "properties": {
                        "b": {
                            "type": "object",
                            "properties": {
                                "c": { "type": "string" }
                            }
                        }
                    }
                }
            }
        }
        """);
        var data = Utf8("""{"a":{"b":{"c":"deep"}}}""");

        schema.TryParse(data, out var result);

        result["/a"]["b"]["c"].HasValue.Should().BeTrue();
        result["/a"]["b"]["c"].GetString().Should().Be("deep");
        result.Dispose();
    }
}
