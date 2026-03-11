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
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests.AllocationTests;

/// <summary>
/// Allocation regression tests for TryParse paths.
/// Asserts that TryParse allocations stay within a tight budget.
///
/// After pooling optimization, both paths should be zero-allocation after warmup:
/// - ErrorCollector uses ArrayPool (no heap alloc after warmup)
/// - ArrayBuffer is pooled via [ThreadStatic] cache (no heap alloc after warmup)
/// - OffsetTable uses ArrayPool (no heap alloc after warmup)
/// Tests guard against regression by enforcing a ceiling.
/// </summary>
[TestFixture]
public class TryParseAllocationTests
{
    private JsonContractSchema _schema = null!;
    private byte[] _payload = null!;

    /// <summary>
    /// Allocation budget for byte[] TryParse path.
    /// After ArrayBuffer pooling: zero heap allocation expected after warmup.
    /// </summary>
    private const long ByteArrayBudget = 64;

    /// <summary>
    /// Allocation budget for ReadOnlySpan TryParse (validate-only) path.
    /// After zero-allocation optimization: zero heap allocation expected.
    /// </summary>
    private const long SpanBudget = 64;

    private const string SchemaJson = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
            },
            "required": ["name", "age"]
        }
        """;

    private const string PayloadJson = """{"name":"test","age":42}""";

    private static long MeasureAllocations(Action action)
    {
        // Warmup: JIT compile + pool priming
        action();
        action();

        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        long after = GC.GetAllocatedBytesForCurrentThread();
        return after - before;
    }

    [OneTimeSetUp]
    public void Setup()
    {
        _schema = JsonContractSchema.Load(SchemaJson)
            ?? throw new InvalidOperationException("Failed to load schema");
        _payload = Encoding.UTF8.GetBytes(PayloadJson);
    }

    [Test]
    public void TryParse_ByteArray_WithinAllocationBudget()
    {
        var bytes = MeasureAllocations(() =>
        {
            _schema.TryParse(_payload, out var result);
            result.Dispose();
        });

        bytes.Should().BeLessThan(ByteArrayBudget,
            "TryParse(byte[]) allocations should be zero after warmup (ArrayBuffer pooled, ArrayPool cached)");
    }

    [Test]
    public void TryParse_ReadOnlySpan_WithinAllocationBudget()
    {
        var bytes = MeasureAllocations(() =>
        {
            _schema.TryParse((ReadOnlySpan<byte>)_payload, out var result);
            result.Dispose();
        });

        bytes.Should().BeLessThan(SpanBudget,
            "TryParse(ReadOnlySpan<byte>) allocations should be zero after warmup (ArrayPool cached)");
    }
}
