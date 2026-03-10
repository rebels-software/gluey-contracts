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

        schema.TryParse(data, out var result);

        result["/tags"][0].HasValue.Should().BeTrue();
        result["/tags"][0].GetString().Should().Be("alpha");
        result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/tags"][1].HasValue.Should().BeTrue();
        result["/tags"][1].GetString().Should().Be("beta");
        // Different from first element
        result["/tags"][1].GetString().Should().NotBe(result["/tags"][0].GetString());
        result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/tags"][99].HasValue.Should().BeFalse();
        result.Dispose();
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

        schema.TryParse(data, out var result);

        // "name" is a string, not an array -- int indexer should return Empty
        result["/name"][0].HasValue.Should().BeFalse();
        result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/items"][0]["name"].HasValue.Should().BeTrue();
        result["/items"][0]["name"].GetString().Should().Be("Widget");
        result["/items"][1]["name"].HasValue.Should().BeTrue();
        result["/items"][1]["name"].GetString().Should().Be("Gadget");
        result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/tags"].Count.Should().Be(3);
        result.Dispose();
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

        schema.TryParse(data, out var result);

        var values = new List<string>();
        foreach (var elem in result["/tags"])
        {
            values.Add(elem.GetString());
        }

        values.Should().BeEquivalentTo(["a", "b", "c"]);
        result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/name"].Count.Should().Be(0);
        result.Dispose();
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

        schema.TryParse(data, out var result);

        var act = () =>
        {
            result.Dispose();
            result.Dispose();
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

        schema.TryParse(data, out var result);

        result["/tags"][-1].HasValue.Should().BeFalse();
        result.Dispose();
    }
}
