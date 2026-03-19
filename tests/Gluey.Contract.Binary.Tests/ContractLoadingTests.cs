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
using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
internal sealed class ContractLoadingTests
{
    // -- Test contract JSON (ADR-16 battery example) --

    private const string BatteryContractJson = """
        {
          "kind": "binary",
          "id": "dunamis/battery/stateUpdate",
          "name": "stateUpdate",
          "version": "0.0.1",
          "displayName": {
            "en-US": "Periodically sent message which carries information about battery state",
            "pl": "Cyklicznie wysylana wiadomosc, ktora niesie informacje o stanie baterii"
          },
          "endianness": "little",
          "fields": {
            "recordedAgo": {
              "type": "uint16",
              "size": 2,
              "displayName": {
                "en-US": "Value representing how many seconds ago data was recorded",
                "pl": "Liczba calkowita reprezentujaca ile sekund temu dane zostaly przeczytane"
              },
              "validation": { "min": 0, "max": 3600 }
            },
            "flags": {
              "dependsOn": "recordedAgo",
              "type": "bits",
              "size": 2,
              "fields": {
                "isCharging": { "bit": 0, "bits": 1, "type": "boolean" },
                "errorCode": { "bit": 1, "bits": 4, "type": "uint8" }
              }
            },
            "level": {
              "dependsOn": "flags",
              "type": "uint8",
              "size": 1,
              "displayName": {
                "en-US": "Integer value indicating percent of battery state",
                "pl": "Liczba calkowita reprezentujaca procent naladowania baterii"
              },
              "validation": { "min": 0, "max": 100 }
            },
            "sensorReading": {
              "dependsOn": "level",
              "type": "int32",
              "size": 3,
              "endianness": "big"
            },
            "operatorBadgeId": {
              "dependsOn": "sensorReading",
              "type": "string",
              "encoding": "ASCII",
              "size": 6,
              "displayName": {
                "en-US": "Unique identifier of forklift operator's badge",
                "pl": "Identyfikator operatora"
              },
              "validation": { "pattern": "^[A-Z0-9]+$" }
            },
            "mode": {
              "dependsOn": "operatorBadgeId",
              "type": "enum",
              "primitive": "uint8",
              "size": 1,
              "values": {
                "0": "idle",
                "1": "charging",
                "2": "discharging"
              }
            },
            "lastThreeVoltages": {
              "dependsOn": "mode",
              "type": "array",
              "count": 3,
              "element": {
                "type": "float32",
                "size": 4
              }
            },
            "errorCount": {
              "dependsOn": "lastThreeVoltages",
              "type": "uint8",
              "size": 1
            },
            "recentErrors": {
              "dependsOn": "errorCount",
              "type": "array",
              "count": "errorCount",
              "element": {
                "type": "struct",
                "size": 5,
                "fields": {
                  "code": {
                    "type": "uint16",
                    "size": 2
                  },
                  "severity": {
                    "dependsOn": "code",
                    "type": "uint8",
                    "size": 1
                  },
                  "timestamp": {
                    "dependsOn": "severity",
                    "type": "uint16",
                    "size": 2,
                    "endianness": "big"
                  }
                }
              }
            },
            "firmwareHash": {
              "dependsOn": "recentErrors",
              "type": "string",
              "encoding": "ASCII",
              "size": 8
            }
          }
        }
        """;

    // -- Helper --

    private static (Dictionary<string, BinaryContractNode>? Fields, string? Endianness, ContractMetadata? Metadata, ErrorCollector Errors) LoadContract(string json)
    {
        var utf8 = Encoding.UTF8.GetBytes(json);
        var errors = new ErrorCollector();
        var (fields, endianness, metadata) = BinaryContractLoader.Load(utf8, errors);
        return (fields, endianness, metadata, errors);
    }

    // -- Valid contract loading --

    [Test]
    public void Load_ValidBatteryContract_ReturnsFieldsWithoutErrors()
    {
        // Arrange & Act
        var (fields, _, _, errors) = LoadContract(BatteryContractJson);

        // Assert
        errors.HasErrors.Should().BeFalse();
        fields.Should().NotBeNull();
    }

    [Test]
    public void Load_ValidBatteryContract_HasCorrectNumberOfTopLevelFields()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        fields.Should().HaveCount(10);
    }

    // -- Invalid JSON --

    [Test]
    public void Load_EmptyString_ReturnsNullWithInvalidJsonError()
    {
        // Arrange & Act
        var (fields, _, _, errors) = LoadContract("");

        // Assert
        fields.Should().BeNull();
        errors.HasErrors.Should().BeTrue();
        errors[0].Code.Should().Be(ValidationErrorCode.InvalidJson);
    }

    [Test]
    public void Load_MalformedJson_ReturnsNullWithInvalidJsonError()
    {
        // Arrange & Act
        var (fields, _, _, errors) = LoadContract("{ not valid json }");

        // Assert
        fields.Should().BeNull();
        errors.HasErrors.Should().BeTrue();
        errors[0].Code.Should().Be(ValidationErrorCode.InvalidJson);
    }

    // -- Missing/wrong kind --

    [Test]
    public void Load_MissingKind_ReturnsNullWithInvalidKindError()
    {
        // Arrange
        const string json = """{ "fields": {} }""";

        // Act
        var (fields, _, _, errors) = LoadContract(json);

        // Assert
        fields.Should().BeNull();
        errors.HasErrors.Should().BeTrue();
        errors[0].Code.Should().Be(ValidationErrorCode.InvalidKind);
    }

    [Test]
    public void Load_WrongKind_ReturnsNullWithInvalidKindError()
    {
        // Arrange
        const string json = """{ "kind": "json-schema", "fields": {} }""";

        // Act
        var (fields, _, _, errors) = LoadContract(json);

        // Assert
        fields.Should().BeNull();
        errors.HasErrors.Should().BeTrue();
        errors[0].Code.Should().Be(ValidationErrorCode.InvalidKind);
    }

    // -- Scalar field mapping --

    [Test]
    public void Load_ScalarField_MapsTypeAndSize()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var recordedAgo = fields!["recordedAgo"];
        recordedAgo.Type.Should().Be("uint16");
        recordedAgo.Size.Should().Be(2);
        recordedAgo.DependsOn.Should().BeNull();
    }

    // -- Bits container mapping --

    [Test]
    public void Load_BitsContainer_MapsTypeAndBitFields()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var flags = fields!["flags"];
        flags.Type.Should().Be("bits");
        flags.Size.Should().Be(2);
        flags.BitFields.Should().NotBeNull();
        flags.BitFields.Should().ContainKey("isCharging");
        flags.BitFields.Should().ContainKey("errorCode");

        var isCharging = flags.BitFields!["isCharging"];
        isCharging.Bit.Should().Be(0);
        isCharging.Bits.Should().Be(1);
        isCharging.Type.Should().Be("boolean");

        var errorCode = flags.BitFields["errorCode"];
        errorCode.Bit.Should().Be(1);
        errorCode.Bits.Should().Be(4);
        errorCode.Type.Should().Be("uint8");
    }

    // -- Enum field mapping --

    [Test]
    public void Load_EnumField_MapsPrimitiveAndValues()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var mode = fields!["mode"];
        mode.Type.Should().Be("enum");
        mode.EnumPrimitive.Should().Be("uint8");
        mode.EnumValues.Should().NotBeNull();
        mode.EnumValues.Should().HaveCount(3);
        mode.EnumValues!["0"].Should().Be("idle");
        mode.EnumValues["1"].Should().Be("charging");
        mode.EnumValues["2"].Should().Be("discharging");
    }

    // -- Array field mapping (fixed count) --

    [Test]
    public void Load_FixedArray_MapsCountAsIntAndElement()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var voltages = fields!["lastThreeVoltages"];
        voltages.Type.Should().Be("array");
        voltages.Count.Should().Be(3);
        voltages.ArrayElement.Should().NotBeNull();
        voltages.ArrayElement!.Type.Should().Be("float32");
        voltages.ArrayElement.Size.Should().Be(4);
    }

    // -- Array field mapping (semi-dynamic count) --

    [Test]
    public void Load_SemiDynamicArray_MapsCountAsString()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var recentErrors = fields!["recentErrors"];
        recentErrors.Type.Should().Be("array");
        recentErrors.Count.Should().Be("errorCount");
    }

    // -- String field mapping --

    [Test]
    public void Load_StringField_MapsEncodingAndSize()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var badge = fields!["operatorBadgeId"];
        badge.Type.Should().Be("string");
        badge.Encoding.Should().Be("ASCII");
        badge.Size.Should().Be(6);
    }

    // -- Struct array element --

    [Test]
    public void Load_StructArrayElement_HasStructFieldsWithCorrectChain()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var recentErrors = fields!["recentErrors"];
        recentErrors.ArrayElement.Should().NotBeNull();
        recentErrors.ArrayElement!.Type.Should().Be("struct");
        recentErrors.ArrayElement.Size.Should().Be(5);
        recentErrors.ArrayElement.StructFields.Should().NotBeNull();
        recentErrors.ArrayElement.StructFields.Should().HaveCount(3);

        // Verify struct sub-fields exist with correct types
        var structFields = recentErrors.ArrayElement.StructFields!;
        structFields.Should().Contain(f => f.Name == "code" && f.Type == "uint16" && f.Size == 2);
        structFields.Should().Contain(f => f.Name == "severity" && f.Type == "uint8" && f.Size == 1);
        structFields.Should().Contain(f => f.Name == "timestamp" && f.Type == "uint16" && f.Size == 2);
    }

    // -- Also verify StructFields on the parent node --

    [Test]
    public void Load_StructArrayElement_ParentNodeHasStructFields()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var recentErrors = fields!["recentErrors"];
        recentErrors.StructFields.Should().NotBeNull();
        recentErrors.StructFields.Should().HaveCount(3);
    }

    // -- Padding field --

    [Test]
    public void Load_PaddingField_MapsTypeAsPadding()
    {
        // Arrange
        const string json = """
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "header": {
                  "type": "uint8",
                  "size": 1
                },
                "gap": {
                  "dependsOn": "header",
                  "type": "padding",
                  "size": 3
                }
              }
            }
            """;

        // Act
        var (fields, _, _, errors) = LoadContract(json);

        // Assert
        errors.HasErrors.Should().BeFalse();
        fields.Should().NotBeNull();
        var gap = fields!["gap"];
        gap.Type.Should().Be("padding");
        gap.Size.Should().Be(3);
    }

    // -- x-error extension --

    [Test]
    public void Load_FieldWithXError_PreservesSchemaErrorInfo()
    {
        // Arrange
        const string json = """
            {
              "kind": "binary",
              "endianness": "little",
              "fields": {
                "level": {
                  "type": "uint8",
                  "size": 1,
                  "x-error": {
                    "code": "LEVEL_INVALID",
                    "title": "Battery level invalid",
                    "detail": "Level must be 0-100",
                    "type": "https://api.example.com/errors/level"
                  }
                }
              }
            }
            """;

        // Act
        var (fields, _, _, errors) = LoadContract(json);

        // Assert
        errors.HasErrors.Should().BeFalse();
        var level = fields!["level"];
        level.ErrorInfo.Should().NotBeNull();
        level.ErrorInfo!.Value.Code.Should().Be("LEVEL_INVALID");
        level.ErrorInfo.Value.Title.Should().Be("Battery level invalid");
        level.ErrorInfo.Value.Detail.Should().Be("Level must be 0-100");
        level.ErrorInfo.Value.Type.Should().Be("https://api.example.com/errors/level");
    }

    // -- Per-field endianness override --

    [Test]
    public void Load_FieldWithEndiannessOverride_PreservesEndianness()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert — sensorReading has endianness "big" override
        var sensor = fields!["sensorReading"];
        sensor.Endianness.Should().Be("big");

        // recordedAgo has no per-field endianness (uses contract default)
        var recordedAgo = fields["recordedAgo"];
        recordedAgo.Endianness.Should().BeNull();
    }

    // -- Validation rules --

    [Test]
    public void Load_FieldWithMinMaxValidation_PreservesValidationRules()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var recordedAgo = fields!["recordedAgo"];
        recordedAgo.Validation.Should().NotBeNull();
        recordedAgo.Validation!.Min.Should().Be(0);
        recordedAgo.Validation.Max.Should().Be(3600);
    }

    [Test]
    public void Load_FieldWithPatternValidation_PreservesValidationRules()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert
        var badge = fields!["operatorBadgeId"];
        badge.Validation.Should().NotBeNull();
        badge.Validation!.Pattern.Should().Be("^[A-Z0-9]+$");
    }

    // -- Contract metadata --

    [Test]
    public void Load_ValidContract_ReturnsMetadata()
    {
        // Arrange & Act
        var (_, endianness, metadata, _) = LoadContract(BatteryContractJson);

        // Assert
        endianness.Should().Be("little");
        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be("dunamis/battery/stateUpdate");
        metadata.Name.Should().Be("stateUpdate");
        metadata.Version.Should().Be("0.0.1");
        metadata.DisplayName.Should().NotBeNull();
        metadata.DisplayName.Should().ContainKey("en-US");
    }

    // -- Struct sub-field endianness --

    [Test]
    public void Load_StructSubFieldWithEndianness_PreservesEndianness()
    {
        // Arrange & Act
        var (fields, _, _, _) = LoadContract(BatteryContractJson);

        // Assert — timestamp inside recentErrors struct has endianness "big"
        var recentErrors = fields!["recentErrors"];
        var timestamp = recentErrors.ArrayElement!.StructFields!.First(f => f.Name == "timestamp");
        timestamp.Endianness.Should().Be("big");
    }
}
