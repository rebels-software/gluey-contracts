// Copyright 2025 Rebels Software sp. z o.o.
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
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        bool success = schema.TryParse(data, out ParseResult result);

        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void TryParse_ValidInput_ResultIsValid()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        schema.TryParse(data, out ParseResult result);

        result.IsValid.Should().BeTrue();
        result.Errors.Count.Should().Be(0);
        result.Dispose();
    }

    [Test]
    public void Parse_ValidInput_ReturnsNonNull()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        ParseResult? result = schema.Parse(data);

        result.Should().NotBeNull();
        result!.Value.IsValid.Should().BeTrue();
        result.Value.Dispose();
    }

    [Test]
    public void TryParse_DoesNotThrow()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        // Direct call -- ReadOnlySpan cannot be captured in lambdas
        bool success = schema.TryParse(data, out var result);

        // If we reached here, no exception was thrown
        success.Should().BeTrue();
        result.Dispose();
    }

    [Test]
    public void Parse_DoesNotThrow()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;

        // Direct call -- ReadOnlySpan cannot be captured in lambdas
        ParseResult? result = schema.Parse(data);

        // If we reached here, no exception was thrown
        result.Should().NotBeNull();
        result!.Value.Dispose();
    }
}
