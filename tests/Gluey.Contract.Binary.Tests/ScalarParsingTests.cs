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

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
public class ScalarParsingTests
{
    // -- GetUInt8 --

    [Test]
    public void GetUInt8_OnBinaryProperty_ReturnsCorrectByte()
    {
        var buffer = new byte[] { 0xAB };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.UInt8);
        prop.GetUInt8().Should().Be(0xAB);
    }

    // -- GetUInt16 --

    [Test]
    public void GetUInt16_LittleEndian_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    [Test]
    public void GetUInt16_BigEndian_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x00, 0x01 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 1, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    // -- GetUInt32 --

    [Test]
    public void GetUInt32_LittleEndian4Bytes_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(0x12345678u);
    }

    [Test]
    public void GetUInt32_BigEndian4Bytes_ReturnsCorrectValue()
    {
        var buffer = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 1, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(0x12345678u);
    }

    [Test]
    public void GetUInt32_BigEndian3Bytes_ZeroPads()
    {
        // [0xFF, 0xCF, 0xC7] -> 16764871 (zero-padded)
        var buffer = new byte[] { 0xFF, 0xCF, 0xC7 };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 1, FieldTypes.UInt32);
        prop.GetUInt32().Should().Be(16764871u);
    }

    // -- GetInt32 truncated --

    [Test]
    public void GetInt32_BigEndian3Bytes_SignExtends()
    {
        // [0xFF, 0xCF, 0xC7] -> -12345 (sign-extended)
        var buffer = new byte[] { 0xFF, 0xCF, 0xC7 };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 1, FieldTypes.Int32);
        prop.GetInt32().Should().Be(-12345);
    }

    [Test]
    public void GetInt32_LittleEndian3Bytes_SignExtends()
    {
        // -12345 in little-endian 3 bytes: 0xC7, 0xCF, 0xFF
        var buffer = new byte[] { 0xC7, 0xCF, 0xFF };
        var prop = new ParsedProperty(buffer, 0, 3, "/test", 1, 0, FieldTypes.Int32);
        prop.GetInt32().Should().Be(-12345);
    }

    // -- Type strictness --

    [Test]
    public void GetInt32_OnUInt16Field_ThrowsInvalidOperationException()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);

        var act = () => prop.GetInt32();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetUInt16_OnUInt16Field_Succeeds()
    {
        var buffer = new byte[] { 0x01, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/test", 1, 0, FieldTypes.UInt16);
        prop.GetUInt16().Should().Be(1);
    }

    // -- GetBoolean --

    [Test]
    public void GetBoolean_NonZero_ReturnsTrue()
    {
        var buffer = new byte[] { 0x01 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.Boolean);
        prop.GetBoolean().Should().BeTrue();
    }

    [Test]
    public void GetBoolean_Zero_ReturnsFalse()
    {
        var buffer = new byte[] { 0x00 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.Boolean);
        prop.GetBoolean().Should().BeFalse();
    }

    [Test]
    public void GetBoolean_OnNonBooleanField_Throws()
    {
        var buffer = new byte[] { 0x01 };
        var prop = new ParsedProperty(buffer, 0, 1, "/test", 1, 0, FieldTypes.UInt8);

        var act = () => prop.GetBoolean();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- GetDouble widening --

    [Test]
    public void GetDouble_OnFloat32Field_WidensCorrectly()
    {
        // float32 = 3.14f in little-endian
        float expected = 3.14f;
        var buffer = new byte[4];
        BitConverter.TryWriteBytes(buffer, expected);
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.Float32);
        prop.GetDouble().Should().BeApproximately(expected, 0.001);
    }

    [Test]
    public void GetDouble_OnUInt32Field_Throws()
    {
        var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 4, "/test", 1, 0, FieldTypes.UInt32);

        var act = () => prop.GetDouble();
        act.Should().Throw<InvalidOperationException>();
    }

    // -- JSON format bypasses type check --

    [Test]
    public void GetInt32_JsonFormat_BypassesTypeCheck()
    {
        // JSON format: _format == 0, no fieldType check
        var buffer = "42"u8.ToArray();
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/test");
        prop.GetInt32().Should().Be(42);
    }
}
