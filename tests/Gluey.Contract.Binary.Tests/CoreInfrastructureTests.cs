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

using System.Buffers.Binary;
using System.Text;
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

/// <summary>
/// Tests targeting Gluey.Contract core infrastructure coverage gaps:
/// ErrorCollector (sentinel, enumerator), ArrayBuffer (growth, bounds),
/// and ParseResult (3-param constructor, enumerator).
/// </summary>
[TestFixture]
internal sealed class CoreInfrastructureTests
{
    // ================================================================
    // ErrorCollector — sentinel on capacity overflow + enumerator
    // ================================================================

    [Test]
    public void Parse_ManyValidationErrors_CollectsAllAndCanEnumerate()
    {
        // Contract with 20 uint8 fields all with max=0 — any non-zero byte produces an error
        var fieldsJson = new StringBuilder();
        fieldsJson.Append('"');
        fieldsJson.Append("f0");
        fieldsJson.Append("\": { \"type\": \"uint8\", \"size\": 1, \"validation\": { \"max\": 0 } }");
        for (int i = 1; i < 20; i++)
        {
            fieldsJson.Append(", \"f");
            fieldsJson.Append(i);
            fieldsJson.Append("\": { \"dependsOn\": \"f");
            fieldsJson.Append(i - 1);
            fieldsJson.Append("\", \"type\": \"uint8\", \"size\": 1, \"validation\": { \"max\": 0 } }");
        }

        var contractJson = $$"""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { {{fieldsJson}} }
            }
            """;
        var schema = BinaryContractSchema.Load(contractJson)!;

        // All bytes are 1, so all 20 fields fail validation
        var payload = new byte[20];
        Array.Fill(payload, (byte)1);

        using var result = schema.Parse(payload)!.Value;

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(20);

        // Exercise ErrorCollector enumerator (foreach)
        int errorCount = 0;
        foreach (var error in result.Errors)
        {
            error.Code.Should().Be(ValidationErrorCode.MaximumExceeded);
            errorCount++;
        }
        errorCount.Should().Be(result.Errors.Count);
    }

    // ================================================================
    // ErrorCollector — indexer out of bounds returns default
    // ================================================================

    [Test]
    public void Parse_Valid_ErrorsIndexerOutOfBounds_ReturnsDefault()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { "v": { "type": "uint8", "size": 1 } }
            }
            """)!;

        using var result = schema.Parse(new byte[] { 42 })!.Value;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
        // Access out of bounds — should return default ValidationError
        var defaultError = result.Errors[99];
        defaultError.Path.Should().BeNull();
    }

    // ================================================================
    // ArrayBuffer — large array triggers element growth
    // ================================================================

    [Test]
    public void Parse_LargeFixedArray_TriggersArrayBufferGrowth()
    {
        // Array with 32 elements — initial ArrayBuffer capacity is 16, so this triggers growth
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 32,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[32];
        for (int i = 0; i < 32; i++)
            payload[i] = (byte)(i + 1);

        using var result = schema.Parse(payload)!.Value;

        result["items"].Count.Should().Be(32);
        result["items"][0].GetUInt8().Should().Be(1);
        result["items"][31].GetUInt8().Should().Be(32);
    }

    // ================================================================
    // ArrayBuffer — many array regions triggers region array growth
    // ================================================================

    [Test]
    public void Parse_ManyArrayFields_TriggersRegionGrowth()
    {
        // 6 separate array fields — initial region capacity is 4, so this triggers region growth
        var fieldsJson = new StringBuilder();
        fieldsJson.Append("\"a0\": { \"type\": \"array\", \"count\": 2, \"element\": { \"type\": \"uint8\", \"size\": 1 } }");
        for (int i = 1; i < 6; i++)
        {
            fieldsJson.Append($", \"a{i}\": {{ \"dependsOn\": \"a{i - 1}\", \"type\": \"array\", \"count\": 2, \"element\": {{ \"type\": \"uint8\", \"size\": 1 }} }}");
        }

        var contractJson = $$"""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": { {{fieldsJson}} }
            }
            """;
        var schema = BinaryContractSchema.Load(contractJson)!;
        var payload = new byte[12]; // 6 arrays × 2 elements
        for (int i = 0; i < 12; i++)
            payload[i] = (byte)(i + 10);

        using var result = schema.Parse(payload)!.Value;

        result["a0"][0].GetUInt8().Should().Be(10);
        result["a5"][1].GetUInt8().Should().Be(21);
    }

    // ================================================================
    // ArrayBuffer — Get with invalid ordinal returns Empty
    // ================================================================

    [Test]
    public void Parse_ArrayElement_OutOfBoundsIndex_ReturnsEmpty()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 2,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;
        var payload = new byte[] { 10, 20 };

        using var result = schema.Parse(payload)!.Value;

        result["items"][99].HasValue.Should().BeFalse();
        result["items"][-1].HasValue.Should().BeFalse();
    }

    // ================================================================
    // ParseResult — foreach enumerator over properties
    // ================================================================

    [Test]
    public void Parse_ForeachOverResult_YieldsFieldsWithValues()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "a": { "type": "uint8", "size": 1 },
                "b": { "dependsOn": "a", "type": "uint16", "size": 2 },
                "gap": { "dependsOn": "b", "type": "padding", "size": 1 },
                "c": { "dependsOn": "gap", "type": "uint8", "size": 1 }
              }
            }
            """)!;
        var payload = new byte[] { 1, 2, 0, 0, 3 };

        using var result = schema.Parse(payload)!.Value;

        // foreach on ParseResult should yield properties with values
        int count = 0;
        foreach (var prop in result)
        {
            prop.HasValue.Should().BeTrue();
            count++;
        }
        // a, b, c have values; gap (padding) does not
        count.Should().Be(3);
    }

    // ================================================================
    // ParseResult — reuse via multiple parses (ArrayBuffer cache)
    // ================================================================

    [Test]
    public void Parse_MultipleTimes_ReusesBuffers()
    {
        var schema = BinaryContractSchema.Load("""
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "items": {
                  "type": "array", "count": 3,
                  "element": { "type": "uint8", "size": 1 }
                }
              }
            }
            """)!;

        // Parse, dispose, parse again — exercises Return/Reset paths
        for (int round = 0; round < 3; round++)
        {
            var payload = new byte[] { (byte)(round * 10 + 1), (byte)(round * 10 + 2), (byte)(round * 10 + 3) };
            using var result = schema.Parse(payload)!.Value;

            result["items"].Count.Should().Be(3);
            result["items"][0].GetUInt8().Should().Be((byte)(round * 10 + 1));
        }
    }
}
