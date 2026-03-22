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

/// <summary>
/// Tests exercising ParsedProperty GetXxx() accessor methods through JSON format parsing.
/// Covers the format=0 (UTF-8/JSON) code paths in ParsedProperty.
/// </summary>
[TestFixture]
public class ParsedPropertyAccessorTests
{
    private static ParseResult? Parse(string schemaJson, string dataJson)
    {
        var schema = JsonContractSchema.Load(schemaJson)!;
        return schema.Parse(Encoding.UTF8.GetBytes(dataJson));
    }

    // ================================================================
    // GetUInt8 — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetUInt8_JsonInteger_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"integer"}}}""",
            """{"v":42}""")!.Value;

        result["v"].GetUInt8().Should().Be(42);
    }

    // ================================================================
    // GetUInt16 — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetUInt16_JsonInteger_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"integer"}}}""",
            """{"v":1234}""")!.Value;

        result["v"].GetUInt16().Should().Be(1234);
    }

    // ================================================================
    // GetUInt32 — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetUInt32_JsonInteger_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"integer"}}}""",
            """{"v":70000}""")!.Value;

        result["v"].GetUInt32().Should().Be(70000u);
    }

    // ================================================================
    // GetInt32 — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetInt32_JsonNegativeInteger_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"integer"}}}""",
            """{"v":-500}""")!.Value;

        result["v"].GetInt32().Should().Be(-500);
    }

    // ================================================================
    // GetInt64 — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetInt64_JsonLargeInteger_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"integer"}}}""",
            """{"v":9999999999}""")!.Value;

        result["v"].GetInt64().Should().Be(9999999999L);
    }

    // ================================================================
    // GetDouble — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetDouble_JsonNumber_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"number"}}}""",
            """{"v":3.14}""")!.Value;

        result["v"].GetDouble().Should().BeApproximately(3.14, 0.001);
    }

    // ================================================================
    // GetBoolean — format=0 path (checks for 't' byte)
    // ================================================================

    [Test]
    public void GetBoolean_JsonTrue_ReturnsTrue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"boolean"}}}""",
            """{"v":true}""")!.Value;

        result["v"].GetBoolean().Should().BeTrue();
    }

    [Test]
    public void GetBoolean_JsonFalse_ReturnsFalse()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"boolean"}}}""",
            """{"v":false}""")!.Value;

        result["v"].GetBoolean().Should().BeFalse();
    }

    // ================================================================
    // GetDecimal — format=0 path (Utf8Parser)
    // ================================================================

    [Test]
    public void GetDecimal_JsonNumber_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"number"}}}""",
            """{"v":99.95}""")!.Value;

        result["v"].GetDecimal().Should().Be(99.95m);
    }

    // ================================================================
    // GetString — format=0 path (UTF-8 decode)
    // ================================================================

    [Test]
    public void GetString_JsonString_ReturnsValue()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"string"}}}""",
            """{"v":"hello"}""")!.Value;

        result["v"].GetString().Should().Be("hello");
    }

    // ================================================================
    // Empty/missing values — all return defaults
    // ================================================================

    [Test]
    public void GetXxx_MissingProperty_ReturnsDefaults()
    {
        using var result = Parse(
            """{"type":"object","properties":{"v":{"type":"string"}}}""",
            """{}""")!.Value;

        result["v"].HasValue.Should().BeFalse();
        result["v"].GetString().Should().BeEmpty();
        result["v"].GetUInt8().Should().Be(0);
        result["v"].GetUInt16().Should().Be(0);
        result["v"].GetUInt32().Should().Be(0u);
        result["v"].GetInt32().Should().Be(0);
        result["v"].GetInt64().Should().Be(0);
        result["v"].GetDouble().Should().Be(0.0);
        result["v"].GetBoolean().Should().BeFalse();
        result["v"].GetDecimal().Should().Be(0m);
    }

    // ================================================================
    // Child/array constructors — exercised via nested objects and arrays
    // ================================================================

    [Test]
    public void NestedObject_ChildTable_ReturnsChildProperties()
    {
        using var result = Parse(
            """{"type":"object","properties":{"obj":{"type":"object","properties":{"a":{"type":"integer"},"b":{"type":"string"}}}}}""",
            """{"obj":{"a":1,"b":"hi"}}""")!.Value;

        result["obj"]["a"].GetInt32().Should().Be(1);
        result["obj"]["b"].GetString().Should().Be("hi");
    }

    [Test]
    public void Array_DirectChildren_ReturnsElements()
    {
        using var result = Parse(
            """{"type":"object","properties":{"arr":{"type":"array","items":{"type":"integer"}}}}""",
            """{"arr":[10,20,30]}""")!.Value;

        result["arr"][0].GetInt32().Should().Be(10);
        result["arr"][1].GetInt32().Should().Be(20);
        result["arr"][2].GetInt32().Should().Be(30);
        result["arr"].Count.Should().Be(3);
    }

    [Test]
    public void ArrayOfObjects_DirectChildren_NestedAccess()
    {
        using var result = Parse(
            """{"type":"object","properties":{"items":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"}}}}}}""",
            """{"items":[{"name":"a"},{"name":"b"}]}""")!.Value;

        result["items"][0]["name"].GetString().Should().Be("a");
        result["items"][1]["name"].GetString().Should().Be("b");
    }
}
