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
public class ArrayElementAccessTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static JsonContractSchema LoadSchema(string json)
        => JsonContractSchema.Load(json)!;

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Basic array element access ──────────────────────────────────────

    [Test]
    public void ArrayElement_FirstElement_ReturnsCorrectValue()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["alpha","beta","gamma"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/tags"][0].HasValue.Should().BeTrue();
        result.Value["/tags"][0].GetString().Should().Be("alpha");
    }

    [Test]
    public void ArrayElement_SecondElement_ReturnsDifferentValue()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["alpha","beta","gamma"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/tags"][1].HasValue.Should().BeTrue();
        result.Value["/tags"][1].GetString().Should().Be("beta");
        // Different from first element
        result.Value["/tags"][1].GetString().Should().NotBe(result.Value["/tags"][0].GetString());
    }

    [Test]
    public void ArrayElement_OutOfBounds_ReturnsEmpty()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["alpha","beta"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/tags"][99].HasValue.Should().BeFalse();
    }

    [Test]
    public void ArrayElement_IntIndexerOnNonArray_ReturnsEmpty()
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

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        // "name" is a string, not an array -- int indexer should return Empty
        result!.Value["/name"][0].HasValue.Should().BeFalse();
    }

    [Test]
    public void ArrayElement_ArrayOfObjects_NestedAccess()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "items": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "price": { "type": "number" }
                        }
                    }
                }
            }
        }
        """);
        var data = Utf8("""{"items":[{"name":"Widget","price":9.99},{"name":"Gadget","price":19.99}]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/items"][0]["name"].HasValue.Should().BeTrue();
        result.Value["/items"][0]["name"].GetString().Should().Be("Widget");
        result.Value["/items"][1]["name"].HasValue.Should().BeTrue();
        result.Value["/items"][1]["name"].GetString().Should().Be("Gadget");
    }

    // ── Array enumeration ──────────────────────────────────────────────

    [Test]
    public void Array_Count_ReturnsElementCount()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["a","b","c"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/tags"].Count.Should().Be(3);
    }

    [Test]
    public void Array_Foreach_YieldsAllElements()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["a","b","c"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        var values = new List<string>();
        foreach (var elem in result!.Value["/tags"])
        {
            values.Add(elem.GetString());
        }

        values.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public void NonArray_Count_ReturnsZero()
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

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/name"].Count.Should().Be(0);
    }

    // ── Double-dispose safety ────────────────────────────────────────────

    [Test]
    public void ParseResult_DoubleDispose_DoesNotThrow()
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

        var result = schema.Parse(data);

        result.Should().NotBeNull();
        var r = result!.Value;
        var act = () =>
        {
            r.Dispose();
            r.Dispose();
        };

        act.Should().NotThrow();
    }

    // ── Negative index ───────────────────────────────────────────────────

    [Test]
    public void ArrayElement_NegativeIndex_ReturnsEmpty()
    {
        var schema = LoadSchema("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);
        var data = Utf8("""{"tags":["alpha"]}""");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value["/tags"][-1].HasValue.Should().BeFalse();
    }
}
