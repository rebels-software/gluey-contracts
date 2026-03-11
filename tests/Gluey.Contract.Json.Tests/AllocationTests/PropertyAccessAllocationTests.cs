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

namespace Gluey.Contract.Json.Tests.AllocationTests;

/// <summary>
/// Allocation regression tests for property access indexers.
/// Ordinal indexer is zero-allocation. String indexer may allocate due to
/// "/" + name concatenation in the fallback path.
/// Note: GetString() allocates by design -- only the indexer lookup is tested.
/// </summary>
[TestFixture]
public class PropertyAccessAllocationTests
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
    public void PropertyAccess_StringIndexer_WithinAllocationBudget()
    {
        // Pre-parse and keep result alive for indexer measurement
        _schema.TryParse(_payload, out var result);

        try
        {
            var bytes = MeasureAllocations(() =>
            {
                var _ = result["name"];
                var __ = result["age"];
            });

            // String indexer may allocate from "/" + name concatenation in fallback path.
            // Budget guards against regression; ordinal path is zero-alloc.
            bytes.Should().BeLessThan(256,
                "String indexer lookup allocations should stay within budget");
        }
        finally
        {
            result.Dispose();
        }
    }

    [Test]
    public void PropertyAccess_OrdinalIndexer_AllocatesZeroBytes()
    {
        _schema.TryParse(_payload, out var result);

        try
        {
            var bytes = MeasureAllocations(() =>
            {
                var _ = result[0];
                var __ = result[1];
            });

            bytes.Should().Be(0, "Ordinal indexer lookup should be zero-allocation");
        }
        finally
        {
            result.Dispose();
        }
    }
}
