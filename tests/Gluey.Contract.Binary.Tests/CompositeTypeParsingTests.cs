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
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

/// <summary>
/// End-to-end integration tests for composite type parsing:
/// COMP-01 (Fixed arrays), COMP-02 (Semi-dynamic arrays),
/// COMP-03 (Struct elements), COMP-05 (Path-based access).
/// </summary>
[TestFixture]
internal sealed class CompositeTypeParsingTests
{
    // ================================================================
    // Contract JSON definitions
    // ================================================================

    // Contract 1: Fixed scalar array (3x uint16 LE)
    // header(uint8,1) + readings(array of 3x uint16,6) = 7 bytes
    private const string FixedScalarArrayLeContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "header":   { "type": "uint8", "size": 1 },
            "readings": { "dependsOn": "header", "type": "array", "count": 3, "element": { "type": "uint16", "size": 2 } }
          }
        }
        """;

    // Contract 2: Fixed scalar array big-endian (3x uint16 BE)
    private const string FixedScalarArrayBeContractJson = """
        {
          "kind": "binary",
          "endianness": "big",
          "fields": {
            "header":   { "type": "uint8", "size": 1 },
            "readings": { "dependsOn": "header", "type": "array", "count": 3, "element": { "type": "uint16", "size": 2 } }
          }
        }
        """;

    // Contract 3: Semi-dynamic scalar array
    // errorCount(uint8,1) + codes(array of errorCount x uint8,var) + trailer(uint16,2)
    private const string SemiDynamicArrayContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "errorCount": { "type": "uint8", "size": 1 },
            "codes":      { "dependsOn": "errorCount", "type": "array", "count": "errorCount", "element": { "type": "uint8", "size": 1 } },
            "trailer":    { "dependsOn": "codes", "type": "uint16", "size": 2 }
          }
        }
        """;

    // Contract 6: Fixed struct array (2 elements, each: code(uint8,1) + severity(uint16,2) = 3 bytes)
    private const string FixedStructArrayContractJson = """
        {
          "kind": "binary",
          "endianness": "little",
          "fields": {
            "errorCount": { "type": "uint8", "size": 1 },
            "errors":     { "dependsOn": "errorCount", "type": "array", "count": 2, "element": { "type": "struct", "size": 3, "fields": {
              "code":     { "type": "uint8", "size": 1 },
              "severity": { "dependsOn": "code", "type": "uint16", "size": 2 }
            }}}
          }
        }
        """;

    // Contract 7: Struct array with big-endian sub-fields
    private const string FixedStructArrayBeContractJson = """
        {
          "kind": "binary",
          "endianness": "big",
          "fields": {
            "errorCount": { "type": "uint8", "size": 1 },
            "errors":     { "dependsOn": "errorCount", "type": "array", "count": 2, "element": { "type": "struct", "size": 3, "fields": {
              "code":     { "type": "uint8", "size": 1 },
              "severity": { "dependsOn": "code", "type": "uint16", "size": 2 }
            }}}
          }
        }
        """;

    // ================================================================
    // COMP-01: Fixed scalar array tests
    // ================================================================

    [Test]
    public void FixedScalarArray_LE_ElementsAccessibleByIndex()
    {
        // COMP-01: Fixed scalar array (3x uint16 LE)
        var schema = BinaryContractSchema.Load(FixedScalarArrayLeContractJson)!;
        // header=0xFF, readings=[100 LE, 200 LE, 300 LE]
        var payload = new byte[] { 0xFF, 0x64, 0x00, 0xC8, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["readings/0"].GetUInt16().Should().Be(100);
        result["readings/1"].GetUInt16().Should().Be(200);
        result["readings/2"].GetUInt16().Should().Be(300);
    }

    [Test]
    public void FixedScalarArray_BE_ElementsAccessibleByIndex()
    {
        // COMP-01: Fixed scalar array big-endian (3x uint16 BE)
        var schema = BinaryContractSchema.Load(FixedScalarArrayBeContractJson)!;
        // header=0xFF, readings=[100 BE, 200 BE, 300 BE]
        var payload = new byte[] { 0xFF, 0x00, 0x64, 0x00, 0xC8, 0x01, 0x2C };

        using var result = schema.Parse(payload)!.Value;

        result["readings/0"].GetUInt16().Should().Be(100);
        result["readings/1"].GetUInt16().Should().Be(200);
        result["readings/2"].GetUInt16().Should().Be(300);
    }

    [Test]
    public void FixedScalarArray_Count_Returns3()
    {
        // COMP-01: Array container Count property
        var schema = BinaryContractSchema.Load(FixedScalarArrayLeContractJson)!;
        var payload = new byte[] { 0xFF, 0x64, 0x00, 0xC8, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["readings"].Count.Should().Be(3);
    }

    [Test]
    public void FixedScalarArray_GetEnumerator_Yields3Elements()
    {
        // COMP-01: GetEnumerator yields elements matching indexed access
        var schema = BinaryContractSchema.Load(FixedScalarArrayLeContractJson)!;
        var payload = new byte[] { 0xFF, 0x64, 0x00, 0xC8, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        var values = new List<ushort>();
        foreach (var element in result["readings"])
        {
            values.Add(element.GetUInt16());
        }

        values.Should().HaveCount(3);
        values.Should().ContainInOrder((ushort)100, (ushort)200, (ushort)300);
    }

    // ================================================================
    // COMP-02: Semi-dynamic array tests
    // ================================================================

    [Test]
    public void SemiDynamicArray_ResolvesCountFromField()
    {
        // COMP-02: Semi-dynamic array with count field "errorCount" = 2
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        // errorCount=2, codes=[10, 20], trailer=1234 LE
        var payload = new byte[] { 0x02, 0x0A, 0x14, 0xD2, 0x04 };

        using var result = schema.Parse(payload)!.Value;

        result["codes/0"].GetUInt8().Should().Be(10);
        result["codes/1"].GetUInt8().Should().Be(20);
        result["codes"].Count.Should().Be(2);
    }

    [Test]
    public void SemiDynamicArray_ZeroCount_EmptyContainer()
    {
        // COMP-02: Zero-count semi-dynamic array
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        // errorCount=0, codes=[], trailer=1234 LE
        var payload = new byte[] { 0x00, 0xD2, 0x04 };

        using var result = schema.Parse(payload)!.Value;

        result["codes"].Count.Should().Be(0);
        var enumerated = new List<ParsedProperty>();
        foreach (var e in result["codes"])
            enumerated.Add(e);
        enumerated.Should().BeEmpty();
    }

    [Test]
    public void SemiDynamicArray_FieldsAfterArray_CorrectOffset()
    {
        // COMP-02: Fields after semi-dynamic array parse at correct offset
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        // errorCount=2, codes=[10, 20], trailer=1234 LE
        var payload = new byte[] { 0x02, 0x0A, 0x14, 0xD2, 0x04 };

        using var result = schema.Parse(payload)!.Value;

        result["trailer"].GetUInt16().Should().Be(1234);
    }

    [Test]
    public void SemiDynamicArray_ZeroCount_TrailerAtCorrectOffset()
    {
        // COMP-02: Zero-count -- parsing continues past empty array
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        // errorCount=0, codes=[], trailer=1234 LE
        var payload = new byte[] { 0x00, 0xD2, 0x04 };

        using var result = schema.Parse(payload)!.Value;

        result["trailer"].GetUInt16().Should().Be(1234);
    }

    [Test]
    public void SemiDynamicArray_TruncatedPayload_ParsesAvailableElements()
    {
        // COMP-02: Truncated payload -- count says 5 but only 3 elements fit
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        // errorCount=5, but only 3 bytes for elements
        var payload = new byte[] { 0x05, 0x0A, 0x14, 0x1E };

        using var result = schema.Parse(payload)!.Value;

        result["codes"].Count.Should().Be(3);
        result["codes/0"].GetUInt8().Should().Be(10);
        result["codes/1"].GetUInt8().Should().Be(20);
        result["codes/2"].GetUInt8().Should().Be(30);
    }

    // ================================================================
    // COMP-03: Struct element tests
    // ================================================================

    [Test]
    public void FixedStructArray_SubFieldAccess()
    {
        // COMP-03: Fixed struct array with sub-field path access
        var schema = BinaryContractSchema.Load(FixedStructArrayContractJson)!;
        // errorCount=2, errors=[{code:1, severity:500 LE}, {code:2, severity:1000 LE}]
        // severity 500 LE = 0xF4,0x01  severity 1000 LE = 0xE8,0x03
        var payload = new byte[] { 0x02, 0x01, 0xF4, 0x01, 0x02, 0xE8, 0x03 };

        using var result = schema.Parse(payload)!.Value;

        result["errors/0/code"].GetUInt8().Should().Be(0x01);
        result["errors/0/severity"].GetUInt16().Should().Be(500);
        result["errors/1/code"].GetUInt8().Should().Be(0x02);
        result["errors/1/severity"].GetUInt16().Should().Be(1000);
    }

    [Test]
    public void FixedStructArray_BigEndian_SubFieldEndiannessRespected()
    {
        // COMP-03: Struct sub-field endianness propagation
        var schema = BinaryContractSchema.Load(FixedStructArrayBeContractJson)!;
        // errorCount=2, errors=[{code:1, severity:500 BE}, {code:2, severity:1000 BE}]
        // severity 500 BE = 0x01,0xF4  severity 1000 BE = 0x03,0xE8
        var payload = new byte[] { 0x02, 0x01, 0x01, 0xF4, 0x02, 0x03, 0xE8 };

        using var result = schema.Parse(payload)!.Value;

        result["errors/0/code"].GetUInt8().Should().Be(0x01);
        result["errors/0/severity"].GetUInt16().Should().Be(500);
        result["errors/1/code"].GetUInt8().Should().Be(0x02);
        result["errors/1/severity"].GetUInt16().Should().Be(1000);
    }

    // ================================================================
    // COMP-05: Path-based access tests
    // ================================================================

    [Test]
    public void PathAccess_ScalarArrayElement()
    {
        // COMP-05: Scalar array path access
        var schema = BinaryContractSchema.Load(FixedScalarArrayLeContractJson)!;
        var payload = new byte[] { 0xFF, 0x64, 0x00, 0xC8, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["readings/2"].GetUInt16().Should().Be(300);
    }

    [Test]
    public void PathAccess_StructArraySubField()
    {
        // COMP-05: Struct array path access
        var schema = BinaryContractSchema.Load(FixedStructArrayContractJson)!;
        var payload = new byte[] { 0x02, 0x01, 0xF4, 0x01, 0x02, 0xE8, 0x03 };

        using var result = schema.Parse(payload)!.Value;

        result["errors/1/code"].GetUInt8().Should().Be(0x02);
    }

    [Test]
    public void PathAccess_ArrayContainer_HasCountGreaterThanZero()
    {
        // COMP-05: Array container path access
        var schema = BinaryContractSchema.Load(FixedStructArrayContractJson)!;
        var payload = new byte[] { 0x02, 0x01, 0xF4, 0x01, 0x02, 0xE8, 0x03 };

        using var result = schema.Parse(payload)!.Value;

        result["errors"].Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void PathAccess_IntegerIndexerOnContainer()
    {
        // COMP-05: Integer indexer on container -- parsed["errors"][0]["code"]
        var schema = BinaryContractSchema.Load(FixedStructArrayContractJson)!;
        var payload = new byte[] { 0x02, 0x01, 0xF4, 0x01, 0x02, 0xE8, 0x03 };

        using var result = schema.Parse(payload)!.Value;

        result["errors"][0]["code"].GetUInt8().Should().Be(0x01);
        result["errors"][1]["severity"].GetUInt16().Should().Be(1000);
    }

    [Test]
    public void PathAccess_NonexistentElement_ReturnsEmpty()
    {
        // COMP-05: Nonexistent path returns Empty
        var schema = BinaryContractSchema.Load(FixedScalarArrayLeContractJson)!;
        var payload = new byte[] { 0xFF, 0x64, 0x00, 0xC8, 0x00, 0x2C, 0x01 };

        using var result = schema.Parse(payload)!.Value;

        result["readings/99"].HasValue.Should().BeFalse();
    }

    [Test]
    public void SemiDynamicArray_Enumeration_YieldsCorrectElements()
    {
        // COMP-02: GetEnumerator on semi-dynamic array
        var schema = BinaryContractSchema.Load(SemiDynamicArrayContractJson)!;
        var payload = new byte[] { 0x02, 0x0A, 0x14, 0xD2, 0x04 };

        using var result = schema.Parse(payload)!.Value;

        var values = new List<byte>();
        foreach (var element in result["codes"])
        {
            values.Add(element.GetUInt8());
        }

        values.Should().HaveCount(2);
        values.Should().ContainInOrder((byte)10, (byte)20);
    }
}
