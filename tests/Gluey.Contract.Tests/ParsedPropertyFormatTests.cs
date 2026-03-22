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

namespace Gluey.Contract.Tests;

[TestFixture]
public class ParsedPropertyFormatTests
{
    private const byte BinaryFormat = 1;
    private const byte LittleEndian = 0;
    private const byte BigEndian = 1;

    [Test]
    public void BinaryGetBoolean_ReturnsTrue_ForByte0x01()
    {
        var buffer = new byte[] { 0x01 };
        var prop = new ParsedProperty(buffer, 0, 1, "/flag", BinaryFormat, LittleEndian, 0);

        prop.GetBoolean().Should().BeTrue();
    }

    [Test]
    public void BinaryGetBoolean_ReturnsFalse_ForByte0x00()
    {
        var buffer = new byte[] { 0x00 };
        var prop = new ParsedProperty(buffer, 0, 1, "/flag", BinaryFormat, LittleEndian, 0);

        prop.GetBoolean().Should().BeFalse();
    }

    [Test]
    public void BinaryGetInt32_ReadsLittleEndian4Bytes()
    {
        var buffer = new byte[] { 0x2A, 0x00, 0x00, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 4, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetInt32().Should().Be(42);
    }

    [Test]
    public void BinaryGetInt32_ReadsBigEndian4Bytes()
    {
        var buffer = new byte[] { 0x00, 0x00, 0x00, 0x2A };
        var prop = new ParsedProperty(buffer, 0, 4, "/val", BinaryFormat, BigEndian, 0);

        prop.GetInt32().Should().Be(42);
    }

    [Test]
    public void BinaryGetInt32_ReadsLittleEndian2Bytes()
    {
        var buffer = new byte[] { 0x2A, 0x00 };
        var prop = new ParsedProperty(buffer, 0, 2, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetInt32().Should().Be(42);
    }

    [Test]
    public void BinaryGetInt32_Reads1ByteSigned()
    {
        var buffer = new byte[] { 0xFE };
        var prop = new ParsedProperty(buffer, 0, 1, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetInt32().Should().Be(-2);
    }

    [Test]
    public void BinaryGetInt64_ReadsLittleEndian8Bytes()
    {
        var buffer = BitConverter.GetBytes(123456789L);
        var prop = new ParsedProperty(buffer, 0, 8, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetInt64().Should().Be(123456789L);
    }

    [Test]
    public void BinaryGetInt64_ReadsBigEndian8Bytes()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, 123456789L);
        var prop = new ParsedProperty(buffer, 0, 8, "/val", BinaryFormat, BigEndian, 0);

        prop.GetInt64().Should().Be(123456789L);
    }

    [Test]
    public void BinaryGetDouble_ReadsLittleEndianIeee754()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, 3.14);
        var prop = new ParsedProperty(buffer, 0, 8, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetDouble().Should().BeApproximately(3.14, 0.001);
    }

    [Test]
    public void BinaryGetDouble_ReadsBigEndianIeee754()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, 3.14);
        var prop = new ParsedProperty(buffer, 0, 8, "/val", BinaryFormat, BigEndian, 0);

        prop.GetDouble().Should().BeApproximately(3.14, 0.001);
    }

    [Test]
    public void BinaryGetString_DecodesUtf8Bytes()
    {
        var buffer = Encoding.UTF8.GetBytes("hello");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/val", BinaryFormat, LittleEndian, 0);

        prop.GetString().Should().Be("hello");
    }

    [Test]
    public void BinaryGetDecimal_ThrowsNotSupportedException()
    {
        var buffer = new byte[16];
        var prop = new ParsedProperty(buffer, 0, 16, "/val", BinaryFormat, LittleEndian, 0);

        var act = () => prop.GetDecimal();

        act.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void DefaultFormat_GetInt32_StillParsesJsonUtf8()
    {
        var buffer = Encoding.UTF8.GetBytes("42");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/val");

        prop.GetInt32().Should().Be(42);
    }

    [Test]
    public void BinaryGetInt32_Reads3ByteTruncated_LittleEndian()
    {
        // 3-byte truncated int32 read with sign extension (little-endian)
        var buffer = new byte[] { 0x01, 0x02, 0x03 };
        var prop = new ParsedProperty(buffer, 0, 3, "/val", BinaryFormat, LittleEndian, 0);

        // LE 3 bytes: fill=0x00 (MSB span[2]=0x03, no sign), (0x00 << 24) | (0x03 << 16) | (0x02 << 8) | 0x01 = 197121
        prop.GetInt32().Should().Be(197121);
    }
}
