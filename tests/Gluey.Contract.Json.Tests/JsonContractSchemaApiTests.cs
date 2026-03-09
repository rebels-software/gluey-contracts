using System.Text;
using FluentAssertions;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class JsonContractSchemaApiTests
{
    private static byte[] SampleJsonBytes => Encoding.UTF8.GetBytes("{\"name\":\"test\"}");

    private static JsonContractSchema CreateSchema() =>
        JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}}}""")!;

    [Test]
    public void TryParse_Compiles_AndReturnsFalse()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        bool success = schema.TryParse(data, out ParseResult result);

        success.Should().BeFalse();
    }

    [Test]
    public void TryParse_OutResult_IsDefault()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        schema.TryParse(data, out ParseResult result);

        // Default ParseResult is valid (no errors) and has no properties
        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
    }

    [Test]
    public void Parse_Compiles_AndReturnsNull()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        ParseResult? result = schema.Parse(data);

        result.Should().BeNull();
    }

    [Test]
    public void TryParse_DoesNotThrow()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        // Direct call -- ReadOnlySpan cannot be captured in lambdas
        bool success = schema.TryParse(data, out _);

        // If we reached here, no exception was thrown
        success.Should().BeFalse();
    }

    [Test]
    public void Parse_DoesNotThrow()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        // Direct call -- ReadOnlySpan cannot be captured in lambdas
        ParseResult? result = schema.Parse(data);

        // If we reached here, no exception was thrown
        result.Should().BeNull();
    }
}
