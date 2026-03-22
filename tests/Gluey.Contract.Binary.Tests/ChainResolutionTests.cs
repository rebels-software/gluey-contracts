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

using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
internal sealed class ChainResolutionTests
{
    // -- Simple chain ordering --

    [Test]
    public void Resolve_ThreeFieldChain_ReturnsCorrectOrder()
    {
        // Arrange: root -> A -> B
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint8", Size = 1 },
            ["A"] = new BinaryContractNode { Name = "A", DependsOn = "root", Type = "uint16", Size = 2 },
            ["B"] = new BinaryContractNode { Name = "B", DependsOn = "A", Type = "uint32", Size = 4 },
        };

        // Act
        var ordered = BinaryChainResolver.Resolve(fields, null);

        // Assert
        ordered.Should().HaveCount(3);
        ordered[0].Name.Should().Be("root");
        ordered[1].Name.Should().Be("A");
        ordered[2].Name.Should().Be("B");
    }

    [Test]
    public void Resolve_FieldsDeclaredOutOfOrder_ResolvesCorrectly()
    {
        // Arrange: declared as B, root, A but chain is root -> A -> B
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["B"] = new BinaryContractNode { Name = "B", DependsOn = "A", Type = "uint32", Size = 4 },
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint8", Size = 1 },
            ["A"] = new BinaryContractNode { Name = "A", DependsOn = "root", Type = "uint16", Size = 2 },
        };

        // Act
        var ordered = BinaryChainResolver.Resolve(fields, null);

        // Assert
        ordered[0].Name.Should().Be("root");
        ordered[1].Name.Should().Be("A");
        ordered[2].Name.Should().Be("B");
    }

    // -- Offset computation --

    [Test]
    public void Resolve_RootField_GetsAbsoluteOffsetZero()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint16", Size = 2 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        ordered[0].AbsoluteOffset.Should().Be(0);
    }

    [Test]
    public void Resolve_SecondField_GetsOffsetEqualToRootSize()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint16", Size = 2 },
            ["second"] = new BinaryContractNode { Name = "second", DependsOn = "root", Type = "uint8", Size = 1 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        ordered[1].AbsoluteOffset.Should().Be(2);
    }

    [Test]
    public void Resolve_ThirdField_GetsOffsetEqualToSumOfPreviousSizes()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint16", Size = 2 },
            ["second"] = new BinaryContractNode { Name = "second", DependsOn = "root", Type = "uint8", Size = 1 },
            ["third"] = new BinaryContractNode { Name = "third", DependsOn = "second", Type = "uint32", Size = 4 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        ordered[2].AbsoluteOffset.Should().Be(3); // 2 + 1
    }

    // -- Fixed array size computation --

    [Test]
    public void Resolve_FixedArray_SizeIsCountTimesElementSize()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["root"] = new BinaryContractNode { Name = "root", DependsOn = null, Type = "uint8", Size = 1 },
            ["arr"] = new BinaryContractNode
            {
                Name = "arr", DependsOn = "root", Type = "array", Size = 0,
                Count = 3, ArrayElement = new ArrayElementInfo("float32", 4, null)
            },
            ["after"] = new BinaryContractNode { Name = "after", DependsOn = "arr", Type = "uint8", Size = 1 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        // arr at offset 1, size = 3 * 4 = 12
        ordered[1].AbsoluteOffset.Should().Be(1);
        ordered[2].AbsoluteOffset.Should().Be(13); // 1 + 12
    }

    // -- Semi-dynamic array and dynamic offset flags --

    [Test]
    public void Resolve_SemiDynamicArray_HasIsDynamicOffsetFalse()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["count"] = new BinaryContractNode { Name = "count", DependsOn = null, Type = "uint8", Size = 1 },
            ["arr"] = new BinaryContractNode
            {
                Name = "arr", DependsOn = "count", Type = "array", Size = 0,
                Count = "count", ArrayElement = new ArrayElementInfo("uint16", 2, null)
            },
            ["after"] = new BinaryContractNode { Name = "after", DependsOn = "arr", Type = "uint8", Size = 1 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        // The semi-dynamic array itself starts at a known offset
        ordered[1].IsDynamicOffset.Should().BeFalse();
    }

    [Test]
    public void Resolve_FieldAfterSemiDynamicArray_HasIsDynamicOffsetTrue()
    {
        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["count"] = new BinaryContractNode { Name = "count", DependsOn = null, Type = "uint8", Size = 1 },
            ["arr"] = new BinaryContractNode
            {
                Name = "arr", DependsOn = "count", Type = "array", Size = 0,
                Count = "count", ArrayElement = new ArrayElementInfo("uint16", 2, null)
            },
            ["after"] = new BinaryContractNode { Name = "after", DependsOn = "arr", Type = "uint8", Size = 1 },
        };

        var ordered = BinaryChainResolver.Resolve(fields, null);

        ordered[2].IsDynamicOffset.Should().BeTrue();
    }

    // -- ADR-16 battery example --

    [Test]
    public void Resolve_AdrBatteryExample_HasCorrectOffsetsAndDynamicFlags()
    {
        var fields = BuildBatteryFields();
        var ordered = BinaryChainResolver.Resolve(fields, "little");

        // Verify ordering
        ordered.Should().HaveCount(10);
        ordered[0].Name.Should().Be("recordedAgo");
        ordered[1].Name.Should().Be("flags");
        ordered[2].Name.Should().Be("level");
        ordered[3].Name.Should().Be("sensorReading");
        ordered[4].Name.Should().Be("operatorBadgeId");
        ordered[5].Name.Should().Be("mode");
        ordered[6].Name.Should().Be("lastThreeVoltages");
        ordered[7].Name.Should().Be("errorCount");
        ordered[8].Name.Should().Be("recentErrors");
        ordered[9].Name.Should().Be("firmwareHash");

        // Verify offsets
        ordered[0].AbsoluteOffset.Should().Be(0);   // recordedAgo
        ordered[1].AbsoluteOffset.Should().Be(2);   // flags
        ordered[2].AbsoluteOffset.Should().Be(4);   // level
        ordered[3].AbsoluteOffset.Should().Be(5);   // sensorReading
        ordered[4].AbsoluteOffset.Should().Be(8);   // operatorBadgeId
        ordered[5].AbsoluteOffset.Should().Be(14);  // mode
        ordered[6].AbsoluteOffset.Should().Be(15);  // lastThreeVoltages (3 * 4 = 12)
        ordered[7].AbsoluteOffset.Should().Be(27);  // errorCount
        ordered[8].AbsoluteOffset.Should().Be(28);  // recentErrors (semi-dynamic)

        // Dynamic offset flags
        ordered[8].IsDynamicOffset.Should().BeFalse(); // recentErrors start is known
        ordered[9].IsDynamicOffset.Should().BeTrue();  // firmwareHash follows semi-dynamic array
    }

    // -- Struct sub-field resolution --

    [Test]
    public void Resolve_StructSubFields_GetRelativeOffsets()
    {
        var structFields = new[]
        {
            new BinaryContractNode { Name = "code", DependsOn = null, Type = "uint16", Size = 2 },
            new BinaryContractNode { Name = "severity", DependsOn = "code", Type = "uint8", Size = 1 },
            new BinaryContractNode { Name = "timestamp", DependsOn = "severity", Type = "uint16", Size = 2, Endianness = "big" },
        };

        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["count"] = new BinaryContractNode { Name = "count", DependsOn = null, Type = "uint8", Size = 1 },
            ["arr"] = new BinaryContractNode
            {
                Name = "arr", DependsOn = "count", Type = "array", Size = 0,
                Count = 2, ArrayElement = new ArrayElementInfo("struct", 5, structFields),
                StructFields = structFields,
            },
        };

        var ordered = BinaryChainResolver.Resolve(fields, "little");

        // Struct sub-fields should have relative offsets (0, 2, 3)
        var arrNode = ordered[1];
        arrNode.ArrayElement!.StructFields.Should().NotBeNull();
        arrNode.ArrayElement.StructFields![0].AbsoluteOffset.Should().Be(0);
        arrNode.ArrayElement.StructFields[1].AbsoluteOffset.Should().Be(2);
        arrNode.ArrayElement.StructFields[2].AbsoluteOffset.Should().Be(3);
    }

    // -- Helper to build ADR-16 battery fields --

    private static Dictionary<string, BinaryContractNode> BuildBatteryFields()
    {
        var structFields = new[]
        {
            new BinaryContractNode { Name = "code", DependsOn = null, Type = "uint16", Size = 2 },
            new BinaryContractNode { Name = "severity", DependsOn = "code", Type = "uint8", Size = 1 },
            new BinaryContractNode { Name = "timestamp", DependsOn = "severity", Type = "uint16", Size = 2, Endianness = "big" },
        };

        return new Dictionary<string, BinaryContractNode>
        {
            ["recordedAgo"] = new BinaryContractNode { Name = "recordedAgo", DependsOn = null, Type = "uint16", Size = 2 },
            ["flags"] = new BinaryContractNode { Name = "flags", DependsOn = "recordedAgo", Type = "bits", Size = 2 },
            ["level"] = new BinaryContractNode { Name = "level", DependsOn = "flags", Type = "uint8", Size = 1 },
            ["sensorReading"] = new BinaryContractNode { Name = "sensorReading", DependsOn = "level", Type = "int32", Size = 3, Endianness = "big" },
            ["operatorBadgeId"] = new BinaryContractNode { Name = "operatorBadgeId", DependsOn = "sensorReading", Type = "string", Size = 6 },
            ["mode"] = new BinaryContractNode { Name = "mode", DependsOn = "operatorBadgeId", Type = "enum", Size = 1 },
            ["lastThreeVoltages"] = new BinaryContractNode
            {
                Name = "lastThreeVoltages", DependsOn = "mode", Type = "array", Size = 0,
                Count = 3, ArrayElement = new ArrayElementInfo("float32", 4, null)
            },
            ["errorCount"] = new BinaryContractNode { Name = "errorCount", DependsOn = "lastThreeVoltages", Type = "uint8", Size = 1 },
            ["recentErrors"] = new BinaryContractNode
            {
                Name = "recentErrors", DependsOn = "errorCount", Type = "array", Size = 0,
                Count = "errorCount", ArrayElement = new ArrayElementInfo("struct", 5, structFields),
                StructFields = structFields,
            },
            ["firmwareHash"] = new BinaryContractNode { Name = "firmwareHash", DependsOn = "recentErrors", Type = "string", Size = 8 },
        };
    }
}
