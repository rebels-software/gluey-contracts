using System.Text;
using FluentAssertions;
using Gluey.Contract;

namespace Gluey.Contract.Tests;

[TestFixture]
public class ParseResultTests
{
    private static ParsedProperty CreateProperty(string value, string path)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return new ParsedProperty(bytes, 0, bytes.Length, path);
    }

    private static (OffsetTable table, ErrorCollector collector, Dictionary<string, int> map)
        CreateTestFixtures(int capacity = 4, bool withErrors = false)
    {
        var table = new OffsetTable(capacity);
        var collector = new ErrorCollector();
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        // Set up some properties
        table.Set(0, CreateProperty("Alice", "/name"));
        table.Set(1, CreateProperty("30", "/age"));
        map["name"] = 0;
        map["age"] = 1;

        if (withErrors)
        {
            collector.Add(new ValidationError("/email", ValidationErrorCode.RequiredMissing, "Required"));
        }

        return (table, collector, map);
    }

    [Test]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        var (table, collector, map) = CreateTestFixtures(withErrors: true);
        using var result = new ParseResult(table, collector, map);

        result.IsValid.Should().BeFalse();
    }

    [Test]
    public void OrdinalIndexer_ValidIndex_ReturnsParsedProperty()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        var prop = result[0];

        prop.HasValue.Should().BeTrue();
        prop.GetString().Should().Be("Alice");
    }

    [Test]
    public void OrdinalIndexer_OutOfRange_ReturnsEmpty()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        var prop = result[999];

        prop.HasValue.Should().BeFalse();
    }

    [Test]
    public void StringIndexer_ValidName_ReturnsParsedProperty()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        var prop = result["name"];

        prop.HasValue.Should().BeTrue();
        prop.GetString().Should().Be("Alice");
    }

    [Test]
    public void StringIndexer_UnknownName_ReturnsEmpty()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        var prop = result["unknown"];

        prop.HasValue.Should().BeFalse();
    }

    [Test]
    public void Errors_ReturnsCollectedErrors()
    {
        var (table, collector, map) = CreateTestFixtures(withErrors: true);
        using var result = new ParseResult(table, collector, map);

        result.Errors[0].Code.Should().Be(ValidationErrorCode.RequiredMissing);
    }

    [Test]
    public void Errors_Count_ZeroWhenValid()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        result.Errors.Count.Should().Be(0);
    }

    [Test]
    public void Errors_Count_GreaterThanZeroWhenInvalid()
    {
        var (table, collector, map) = CreateTestFixtures(withErrors: true);
        using var result = new ParseResult(table, collector, map);

        result.Errors.Count.Should().BeGreaterThan(0);
    }

    [Test]
    public void GetEnumerator_AllowsForeach()
    {
        var (table, collector, map) = CreateTestFixtures();
        using var result = new ParseResult(table, collector, map);

        var properties = new List<ParsedProperty>();
        foreach (var prop in result)
        {
            properties.Add(prop);
        }

        // Only 2 out of 4 slots have values set
        properties.Should().HaveCount(2);
        properties[0].GetString().Should().Be("Alice");
        properties[1].GetString().Should().Be("30");
    }

    [Test]
    public void Dispose_CascadesToOffsetTableAndErrorCollector()
    {
        var (table, collector, map) = CreateTestFixtures();
        var result = new ParseResult(table, collector, map);

        // Should not throw
        var dispose = () => result.Dispose();
        dispose.Should().NotThrow();
    }

    [Test]
    public void ParseResult_IsReadonlyStruct()
    {
        typeof(ParseResult).IsValueType.Should().BeTrue();
        typeof(ParseResult).GetInterfaces().Should().Contain(typeof(IDisposable));
    }

    [Test]
    public void Default_ParseResult_IsValid()
    {
        // Default (uninitialized) ParseResult should not throw and should be "valid" (no errors)
        ParseResult result = default;

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
        result[0].HasValue.Should().BeFalse();
        result["anything"].HasValue.Should().BeFalse();
    }

    [Test]
    public void Default_ParseResult_Dispose_DoesNotThrow()
    {
        ParseResult result = default;

        var dispose = () => result.Dispose();
        dispose.Should().NotThrow();
    }

    [Test]
    public void Default_ParseResult_Foreach_DoesNotThrow()
    {
        ParseResult result = default;

        var properties = new List<ParsedProperty>();
        foreach (var prop in result)
        {
            properties.Add(prop);
        }

        properties.Should().BeEmpty();
    }
}
