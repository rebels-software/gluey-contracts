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
internal sealed class ContractValidationTests
{
    // -- Helpers --

    private static Dictionary<string, BinaryContractNode> Fields(params BinaryContractNode[] nodes)
    {
        var dict = new Dictionary<string, BinaryContractNode>(nodes.Length);
        foreach (var n in nodes)
            dict[n.Name] = n;
        return dict;
    }

    private static BinaryContractNode Node(string name, string type = "uint8", int size = 1, string? dependsOn = null,
        Dictionary<string, BitFieldInfo>? bitFields = null,
        ArrayElementInfo? arrayElement = null,
        object? count = null)
    {
        return new BinaryContractNode
        {
            Name = name,
            Type = type,
            Size = size,
            DependsOn = dependsOn,
            BitFields = bitFields,
            ArrayElement = arrayElement,
            Count = count,
        };
    }

    // -- Graph validation: root field --

    [Test]
    public void Validate_SingleRoot_Passes()
    {
        // Arrange
        var fields = Fields(
            Node("root"),
            Node("child", dependsOn: "root"));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeTrue();
        errors.HasErrors.Should().BeFalse();
    }

    [Test]
    public void Validate_ZeroRoots_ReturnsMissingRootError()
    {
        // Arrange — all fields have dependsOn, no root
        var fields = Fields(
            Node("a", dependsOn: "b"),
            Node("b", dependsOn: "a"));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeFalse();
        errors.Count.Should().BeGreaterThanOrEqualTo(1);
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.MissingRoot);
    }

    [Test]
    public void Validate_TwoRoots_ReturnsMissingRootError()
    {
        // Arrange — two fields without dependsOn
        var fields = Fields(
            Node("rootA"),
            Node("rootB"));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeFalse();
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.MissingRoot);
    }

    // -- Graph validation: cycles --

    [Test]
    public void Validate_SimpleCycle_ReturnsCyclicDependencyError()
    {
        // Arrange — A depends on B, B depends on A
        var fields = Fields(
            Node("a", dependsOn: "b"),
            Node("b", dependsOn: "a"));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.CyclicDependency);
    }

    [Test]
    public void Validate_LongerCycle_ReturnsCyclicDependencyError()
    {
        // Arrange — A->B->C->A (all have dependsOn, also zero roots)
        var fields = Fields(
            Node("a", dependsOn: "c"),
            Node("b", dependsOn: "a"),
            Node("c", dependsOn: "b"));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.CyclicDependency);
    }

    // -- Graph validation: shared parent --

    [Test]
    public void Validate_SharedParent_ReturnsSharedParentError()
    {
        // Arrange — both Y and Z depend on X
        var fields = Fields(
            Node("x"),
            Node("y", dependsOn: "x"),
            Node("z", dependsOn: "x"));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.SharedParent);
    }

    // -- Graph validation: invalid reference --

    [Test]
    public void Validate_DependsOnNonExistentField_ReturnsInvalidReferenceError()
    {
        // Arrange
        var fields = Fields(
            Node("root"),
            Node("child", dependsOn: "nonexistent"));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.InvalidReference);
    }

    // -- Graph validation: valid chain --

    [Test]
    public void Validate_ValidChain_Passes()
    {
        // Arrange — root->A->B->C
        var fields = Fields(
            Node("root"),
            Node("a", dependsOn: "root"),
            Node("b", dependsOn: "a"),
            Node("c", dependsOn: "b"));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeTrue();
        errors.HasErrors.Should().BeFalse();
    }

    // -- Size validation --

    [Test]
    public void Validate_FieldWithZeroSize_ReturnsMissingSizeError()
    {
        // Arrange
        var fields = Fields(
            Node("root", size: 0));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.MissingSize);
    }

    [Test]
    public void Validate_AllFieldsWithExplicitSize_PassesSizeCheck()
    {
        // Arrange
        var fields = Fields(
            Node("root", size: 2),
            Node("child", size: 4, dependsOn: "root"));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeTrue();
    }

    // -- Bit field validation --

    [Test]
    public void Validate_NonOverlappingBitFields_Passes()
    {
        // Arrange — 2-byte (16-bit) container, bit 0 width 4, bit 4 width 4: no overlap
        var bitFields = new Dictionary<string, BitFieldInfo>
        {
            ["low"] = new BitFieldInfo(0, 4, "uint8"),
            ["high"] = new BitFieldInfo(4, 4, "uint8"),
        };
        var fields = Fields(
            Node("flags", type: "bits", size: 2, bitFields: bitFields));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Validate_OverlappingBitFields_ReturnsOverlappingBitsError()
    {
        // Arrange — bit 0 width 4 and bit 3 width 2 overlap at bit 3
        var bitFields = new Dictionary<string, BitFieldInfo>
        {
            ["a"] = new BitFieldInfo(0, 4, "uint8"),
            ["b"] = new BitFieldInfo(3, 2, "uint8"),
        };
        var fields = Fields(
            Node("flags", type: "bits", size: 2, bitFields: bitFields));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.OverlappingBits);
    }

    [Test]
    public void Validate_BitFieldExceedingContainer_ReturnsOverlappingBitsError()
    {
        // Arrange — 1-byte (8-bit) container, bit 6 width 4 => end bit 9 > 8
        var bitFields = new Dictionary<string, BitFieldInfo>
        {
            ["overflow"] = new BitFieldInfo(6, 4, "uint8"),
        };
        var fields = Fields(
            Node("flags", type: "bits", size: 1, bitFields: bitFields));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.OverlappingBits);
    }

    // -- Array count validation --

    [Test]
    public void Validate_SemiDynamicArrayCountReferencingValidNumericField_Passes()
    {
        // Arrange
        var fields = Fields(
            Node("count", type: "uint8", size: 1),
            Node("items", type: "array", size: 4, dependsOn: "count",
                count: "count",
                arrayElement: new ArrayElementInfo("uint8", 1, null)));
        var errors = new ErrorCollector();

        // Act
        bool result = BinaryContractValidator.Validate(fields, errors);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Validate_SemiDynamicArrayCountReferencingNonExistentField_ReturnsInvalidReferenceError()
    {
        // Arrange
        var fields = Fields(
            Node("root"),
            Node("items", type: "array", size: 4, dependsOn: "root",
                count: "missing",
                arrayElement: new ArrayElementInfo("uint8", 1, null)));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.InvalidReference);
    }

    [Test]
    public void Validate_SemiDynamicArrayCountReferencingStringField_ReturnsInvalidReferenceError()
    {
        // Arrange — count references a string-type field (non-numeric)
        var fields = Fields(
            Node("label", type: "string", size: 10),
            Node("items", type: "array", size: 4, dependsOn: "label",
                count: "label",
                arrayElement: new ArrayElementInfo("uint8", 1, null)));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.InvalidReference);
    }

    // -- Error collection (not fail-fast) --

    [Test]
    public void Validate_MultipleErrors_CollectsAll()
    {
        // Arrange — missing root (zero roots) AND missing size
        var fields = Fields(
            Node("a", size: 0, dependsOn: "b"),
            Node("b", size: 1, dependsOn: "a"));
        var errors = new ErrorCollector();

        // Act
        BinaryContractValidator.Validate(fields, errors);

        // Assert — should have at least 2 errors (MissingRoot and MissingSize, plus possibly CyclicDependency)
        errors.Count.Should().BeGreaterThanOrEqualTo(2);
        var codes = Enumerable.Range(0, errors.Count).Select(i => errors[i].Code).ToList();
        codes.Should().Contain(ValidationErrorCode.MissingRoot);
        codes.Should().Contain(ValidationErrorCode.MissingSize);
    }
}
