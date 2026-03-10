using System.Text;
using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class SchemaOptionsTests
{
    private const string SimpleSchema = """{"type":"object","properties":{"name":{"type":"string"}}}""";

    // ── SchemaOptions defaults ───────────────────────────────────────────

    [Test]
    public void SchemaOptions_Default_AssertFormatIsFalse()
    {
        var options = new SchemaOptions();
        options.AssertFormat.Should().BeFalse();
    }

    [Test]
    public void SchemaOptions_AssertFormatTrue_SetsProperty()
    {
        var options = new SchemaOptions { AssertFormat = true };
        options.AssertFormat.Should().BeTrue();
    }

    // ── TryLoad without options (backward compat) ───────────────────────

    [Test]
    public void TryLoad_Bytes_WithoutOptions_Works()
    {
        var bytes = Encoding.UTF8.GetBytes(SimpleSchema);
        var result = JsonContractSchema.TryLoad(bytes, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeFalse();
    }

    [Test]
    public void TryLoad_String_WithoutOptions_Works()
    {
        var result = JsonContractSchema.TryLoad(SimpleSchema, out var schema);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeFalse();
    }

    // ── TryLoad with options ────────────────────────────────────────────

    [Test]
    public void TryLoad_Bytes_WithAssertFormatTrue_SetsFlag()
    {
        var bytes = Encoding.UTF8.GetBytes(SimpleSchema);
        var options = new SchemaOptions { AssertFormat = true };
        var result = JsonContractSchema.TryLoad(bytes, out var schema, options: options);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeTrue();
    }

    [Test]
    public void TryLoad_String_WithAssertFormatTrue_SetsFlag()
    {
        var options = new SchemaOptions { AssertFormat = true };
        var result = JsonContractSchema.TryLoad(SimpleSchema, out var schema, options: options);

        result.Should().BeTrue();
        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeTrue();
    }

    // ── Load without options (backward compat) ──────────────────────────

    [Test]
    public void Load_Bytes_WithoutOptions_Works()
    {
        var bytes = Encoding.UTF8.GetBytes(SimpleSchema);
        var schema = JsonContractSchema.Load(bytes);

        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeFalse();
    }

    [Test]
    public void Load_String_WithoutOptions_Works()
    {
        var schema = JsonContractSchema.Load(SimpleSchema);

        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeFalse();
    }

    // ── Load with options ───────────────────────────────────────────────

    [Test]
    public void Load_Bytes_WithAssertFormatTrue_SetsFlag()
    {
        var bytes = Encoding.UTF8.GetBytes(SimpleSchema);
        var options = new SchemaOptions { AssertFormat = true };
        var schema = JsonContractSchema.Load(bytes, options: options);

        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeTrue();
    }

    [Test]
    public void Load_String_WithAssertFormatTrue_SetsFlag()
    {
        var options = new SchemaOptions { AssertFormat = true };
        var schema = JsonContractSchema.Load(SimpleSchema, options: options);

        schema.Should().NotBeNull();
        schema!.AssertFormat.Should().BeTrue();
    }
}
