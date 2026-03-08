using System.Text;

namespace Gluey.Contract.Tests;

[TestFixture]
public class ParsedPropertyTests
{
    [Test]
    public void Default_HasValue_IsFalse()
    {
        var prop = new ParsedProperty();
        prop.HasValue.Should().BeFalse();
    }

    [Test]
    public void Default_Path_IsEmpty()
    {
        var prop = new ParsedProperty();
        prop.Path.Should().BeEmpty();
    }

    [Test]
    public void Empty_HasValue_IsFalse()
    {
        ParsedProperty.Empty.HasValue.Should().BeFalse();
    }

    [Test]
    public void Constructed_HasValue_IsTrue()
    {
        var buffer = Encoding.UTF8.GetBytes("hello");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/name");

        prop.HasValue.Should().BeTrue();
    }

    [Test]
    public void Constructed_Path_ReturnsCorrectPath()
    {
        var buffer = Encoding.UTF8.GetBytes("hello");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/name");

        prop.Path.Should().Be("/name");
    }

    [Test]
    public void RawBytes_ReturnsCorrectSlice()
    {
        var buffer = Encoding.UTF8.GetBytes("XXhelloYY");
        var prop = new ParsedProperty(buffer, 2, 5, "/val");

        prop.RawBytes.ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
    }

    [Test]
    public void GetString_ReturnsUtf8DecodedString()
    {
        var buffer = Encoding.UTF8.GetBytes("hello world");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/val");

        prop.GetString().Should().Be("hello world");
    }

    [Test]
    public void GetString_OnEmpty_ReturnsEmptyString()
    {
        var prop = ParsedProperty.Empty;
        prop.GetString().Should().BeEmpty();
    }

    [Test]
    public void GetInt32_ParsesIntegerFromBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("42");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/age");

        prop.GetInt32().Should().Be(42);
    }

    [Test]
    public void GetInt64_ParsesLongFromBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("9999999999");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/bigId");

        prop.GetInt64().Should().Be(9999999999L);
    }

    [Test]
    public void GetDouble_ParsesDoubleFromBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("3.14");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/pi");

        prop.GetDouble().Should().BeApproximately(3.14, 0.001);
    }

    [Test]
    public void GetBoolean_ReturnsTrue_ForTrueBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("true");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/flag");

        prop.GetBoolean().Should().BeTrue();
    }

    [Test]
    public void GetBoolean_ReturnsFalse_ForFalseBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("false");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/flag");

        prop.GetBoolean().Should().BeFalse();
    }

    [Test]
    public void GetDecimal_ParsesDecimalFromBytes()
    {
        var buffer = Encoding.UTF8.GetBytes("123.456");
        var prop = new ParsedProperty(buffer, 0, buffer.Length, "/price");

        prop.GetDecimal().Should().Be(123.456m);
    }

    [Test]
    public void GetInt32_OnEmpty_ReturnsDefault()
    {
        ParsedProperty.Empty.GetInt32().Should().Be(default(int));
    }

    [Test]
    public void GetInt64_OnEmpty_ReturnsDefault()
    {
        ParsedProperty.Empty.GetInt64().Should().Be(default(long));
    }

    [Test]
    public void GetDouble_OnEmpty_ReturnsDefault()
    {
        ParsedProperty.Empty.GetDouble().Should().Be(default(double));
    }

    [Test]
    public void GetBoolean_OnEmpty_ReturnsDefault()
    {
        ParsedProperty.Empty.GetBoolean().Should().Be(default(bool));
    }

    [Test]
    public void GetDecimal_OnEmpty_ReturnsDefault()
    {
        ParsedProperty.Empty.GetDecimal().Should().Be(default(decimal));
    }

    [Test]
    public void RawBytes_OnDefault_ReturnsEmptySpan()
    {
        var prop = new ParsedProperty();
        prop.RawBytes.Length.Should().Be(0);
    }

    [Test]
    public void GetString_WithOffset_ReturnsCorrectSubstring()
    {
        var buffer = Encoding.UTF8.GetBytes("XXhelloYY");
        var prop = new ParsedProperty(buffer, 2, 5, "/val");

        prop.GetString().Should().Be("hello");
    }
}
