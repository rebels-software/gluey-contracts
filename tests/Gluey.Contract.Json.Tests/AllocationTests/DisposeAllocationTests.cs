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
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests.AllocationTests;

/// <summary>
/// Allocation regression tests for ParseResult.Dispose().
/// Dispose itself is zero-allocation (returns buffers to ArrayPool).
/// Double-dispose is also zero-allocation (idempotent).
///
/// Note: Dispose is tested in isolation (on an already-parsed result).
/// The parse call itself has known allocations (ErrorCollector int[1], ArrayBuffer).
/// </summary>
[TestFixture]
public class DisposeAllocationTests
{
    private JsonContractSchema _schema = null!;
    private byte[] _payload = null!;

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
    public void Dispose_AllocatesZeroBytes()
    {
        // Pre-parse to have a result to dispose
        _schema.TryParse(_payload, out var result);

        var bytes = MeasureAllocations(() =>
        {
            result.Dispose();
        });

        bytes.Should().Be(0, "Dispose should be zero-allocation (ArrayPool return only)");
    }

    [Test]
    public void DoubleDispose_AllocatesZeroBytes()
    {
        _schema.TryParse(_payload, out var result);
        result.Dispose(); // First dispose

        var bytes = MeasureAllocations(() =>
        {
            result.Dispose(); // Second dispose -- should be no-op and zero-alloc
        });

        bytes.Should().Be(0, "Double Dispose should be zero-allocation (idempotent)");
    }
}
