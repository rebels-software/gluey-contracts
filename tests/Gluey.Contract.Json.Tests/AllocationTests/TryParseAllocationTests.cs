using System.Text;
using FluentAssertions;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests.AllocationTests;

/// <summary>
/// Allocation regression tests for TryParse paths.
/// Asserts that TryParse allocations stay within a tight budget.
///
/// Known allocations per parse call:
/// - ErrorCollector: int[1] count holder (~32 bytes)
/// - ArrayBuffer: class instance for byte[] path (~48 bytes)
/// These are structural allocations from the walker; the validation/indexing paths are zero-alloc.
/// Tests guard against regression by enforcing a ceiling.
/// </summary>
[TestFixture]
public class TryParseAllocationTests
{
    private JsonContractSchema _schema = null!;
    private byte[] _payload = null!;

    /// <summary>
    /// Allocation budget for byte[] TryParse path.
    /// Accounts for ErrorCollector int[1] + ArrayBuffer class instance.
    /// </summary>
    private const long ByteArrayBudget = 1024;

    /// <summary>
    /// Allocation budget for ReadOnlySpan TryParse (validate-only) path.
    /// Accounts for ErrorCollector int[1] only (no ArrayBuffer/OffsetTable on span path).
    /// </summary>
    private const long SpanBudget = 512;

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
            "TryParse(byte[]) allocations should stay within budget (ErrorCollector int[1] + ArrayBuffer instance)");
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
            "TryParse(ReadOnlySpan<byte>) allocations should stay within budget (ErrorCollector int[1] only)");
    }
}
