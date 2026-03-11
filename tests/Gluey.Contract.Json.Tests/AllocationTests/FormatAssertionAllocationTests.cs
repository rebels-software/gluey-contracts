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

namespace Gluey.Contract.Json.Tests.AllocationTests;

/// <summary>
/// Allocation budget tests for format assertion paths.
/// Format assertion is an opt-in feature (SchemaOptions.AssertFormat = true) that
/// may allocate due to string conversions for .NET parser APIs. This is a documented
/// exception to the zero-allocation guarantee.
/// </summary>
[TestFixture]
public class FormatAssertionAllocationTests
{
    private JsonContractSchema _schema = null!;
    private byte[] _payload = null!;

    private const string SchemaJson = """
        {
            "type": "object",
            "properties": {
                "email": { "type": "string", "format": "email" },
                "date": { "type": "string", "format": "date" },
                "uri": { "type": "string", "format": "uri" }
            },
            "required": ["email", "date", "uri"]
        }
        """;

    private const string PayloadJson = """{"email":"user@example.com","date":"2026-01-15","uri":"https://example.com"}""";

    [OneTimeSetUp]
    public void Setup()
    {
        var options = new SchemaOptions { AssertFormat = true };
        _schema = JsonContractSchema.Load(SchemaJson, options: options)
            ?? throw new InvalidOperationException("Failed to load schema with format assertion");
        _payload = Encoding.UTF8.GetBytes(PayloadJson);
    }

    [Test]
    public void TryParse_WithFormatAssertion_AllocationBudget()
    {
        // Warmup: JIT + pool priming
        _schema.TryParse(_payload, out var warmup);
        warmup.Dispose();
        _schema.TryParse(_payload, out var warmup2);
        warmup2.Dispose();

        long before = GC.GetAllocatedBytesForCurrentThread();
        _schema.TryParse(_payload, out var result);
        result.Dispose();
        long after = GC.GetAllocatedBytesForCurrentThread();
        long allocated = after - before;

        // Format assertion is allowed to allocate, but should be reasonable.
        // The budget accounts for string conversions needed by format validators.
        allocated.Should().BeLessThan(2000,
            "Format assertion allocations should be bounded -- string conversions for format validators are the documented exception to zero-alloc");
    }
}
