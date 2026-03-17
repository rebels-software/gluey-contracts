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
    public void Parse_ValidInput_ReturnsNonNull()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_ValidInput_ResultIsValid()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value.Errors.Count.Should().Be(0);
    }

    [Test]
    public void Parse_DoesNotThrow()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        // Direct call -- ReadOnlySpan cannot be captured in lambdas
        using var result = schema.Parse(data);

        // If we reached here, no exception was thrown
        result.Should().NotBeNull();
    }

    // ── Parse byte[] overload ─────────────────────────────────────

    [Test]
    public void Parse_ByteArray_ValidInput_ReturnsResult()
    {
        var schema = CreateSchema();
        var data = SampleJsonBytes;

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value["/name"].HasValue.Should().BeTrue();
    }

    [Test]
    public void Parse_ByteArray_InvalidInput_ReturnsResultWithErrors()
    {
        var schema = JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""")!;
        var data = Encoding.UTF8.GetBytes("{}");

        using var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
    }

    [Test]
    public void Parse_ByteArray_MalformedJson_ReturnsNull()
    {
        var schema = CreateSchema();
        var data = Encoding.UTF8.GetBytes("{bad json");

        var result = schema.Parse(data);

        result.Should().BeNull();
    }

    [Test]
    public void Parse_ByteArray_InvalidSchema_ReturnsResultWithErrorDetails()
    {
        var schema = JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""")!;
        var data = Encoding.UTF8.GetBytes("{}");

        var result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeFalse();
        result.Value.Errors.Count.Should().BeGreaterThan(0);
        result.Value.Dispose();
    }

    // ── Parse Span overload with malformed JSON ───────────────────

    [Test]
    public void Parse_Span_MalformedJson_ReturnsNull()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes("{bad json");

        var result = schema.Parse(data);

        result.Should().BeNull();
    }

    // ── TryLoad with SchemaOptions ──────────────────────────────────

    [Test]
    public void TryLoad_WithAssertFormat_SetsFlag()
    {
        var options = new SchemaOptions { AssertFormat = true };
        var success = JsonContractSchema.TryLoad("""{"type":"string","format":"email"}""", out var schema, options: options);

        success.Should().BeTrue();
        schema!.AssertFormat.Should().BeTrue();
    }

    [Test]
    public void TryLoad_WithoutOptions_AssertFormatIsFalse()
    {
        var success = JsonContractSchema.TryLoad("""{"type":"string","format":"email"}""", out var schema);

        success.Should().BeTrue();
        schema!.AssertFormat.Should().BeFalse();
    }

    // ── Load string overload ────────────────────────────────────────

    [Test]
    public void Load_FromString_InvalidJson_ReturnsNull()
    {
        var result = JsonContractSchema.Load("not valid json");

        result.Should().BeNull();
    }

    [Test]
    public void TryLoad_FromString_InvalidJson_ReturnsFalse()
    {
        var success = JsonContractSchema.TryLoad("not valid json", out var schema);

        success.Should().BeFalse();
        schema.Should().BeNull();
    }

    // ── PropertyCount ───────────────────────────────────────────────

    [Test]
    public void PropertyCount_EmptySchema_IsZero()
    {
        var schema = JsonContractSchema.Load("""{"type":"object"}""");

        schema.Should().NotBeNull();
        schema!.PropertyCount.Should().Be(0);
    }
}
