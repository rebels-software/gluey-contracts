using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Gluey.Contract.Json;
using Gluey.Contract.Benchmarks.Payloads;

namespace Gluey.Contract.Benchmarks.Scenarios;

/// <summary>
/// Benchmarks TryParse and validate-only paths against a complex schema exercising
/// allOf/anyOf, if/then/else, pattern, required, min/max constraints.
/// Includes System.Text.Json baseline for comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class FullSchemaBenchmark
{
    private JsonContractSchema _schema = null!;
    private byte[] _smallPayload = null!;
    private byte[] _mediumPayload = null!;
    private byte[] _largePayload = null!;

    private const string SchemaJson = """
        {
            "type": "object",
            "properties": {
                "type": { "type": "string", "enum": ["basic", "premium"] },
                "name": { "type": "string", "minLength": 1, "maxLength": 100 },
                "age": { "type": "integer", "minimum": 0, "maximum": 150 },
                "email": { "type": "string", "format": "email" },
                "phone": { "type": "string" },
                "rating": { "type": "number", "minimum": 0, "maximum": 5 },
                "code": { "type": "string", "pattern": "^[A-Z]{3}-[0-9]{3}$" },
                "billing": {
                    "type": "object",
                    "properties": {
                        "method": { "type": "string" },
                        "cardNumber": { "type": "string" }
                    },
                    "required": ["method"]
                },
                "shipping": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" },
                        "zip": { "type": "string" }
                    },
                    "required": ["street", "city", "zip"]
                }
            },
            "required": ["type", "name", "age", "email"],
            "allOf": [
                {
                    "if": {
                        "properties": { "type": { "const": "premium" } },
                        "required": ["type"]
                    },
                    "then": {
                        "required": ["billing", "shipping", "phone"]
                    }
                }
            ],
            "additionalProperties": { "type": "string" }
        }
        """;

    [GlobalSetup]
    public void Setup()
    {
        _schema = JsonContractSchema.Load(SchemaJson)
            ?? throw new InvalidOperationException("Failed to load full schema");

        _smallPayload = PayloadGenerator.GenerateFullSchema(100);
        _mediumPayload = PayloadGenerator.GenerateFullSchema(5_000);
        _largePayload = PayloadGenerator.GenerateFullSchema(50_000);
    }

    // ── TryParse (byte[] overload -- full path with OffsetTable) ──

    [Benchmark]
    public bool TryParse_Small()
    {
        var ok = _schema.TryParse(_smallPayload, out var result);
        result.Dispose();
        return ok;
    }

    [Benchmark]
    public bool TryParse_Medium()
    {
        var ok = _schema.TryParse(_mediumPayload, out var result);
        result.Dispose();
        return ok;
    }

    [Benchmark]
    public bool TryParse_Large()
    {
        var ok = _schema.TryParse(_largePayload, out var result);
        result.Dispose();
        return ok;
    }

    // ── ValidateOnly (ReadOnlySpan<byte> overload) ──

    [Benchmark]
    public bool ValidateOnly_Small()
    {
        var ok = _schema.TryParse((ReadOnlySpan<byte>)_smallPayload, out var result);
        result.Dispose();
        return ok;
    }

    [Benchmark]
    public bool ValidateOnly_Medium()
    {
        var ok = _schema.TryParse((ReadOnlySpan<byte>)_mediumPayload, out var result);
        result.Dispose();
        return ok;
    }

    [Benchmark]
    public bool ValidateOnly_Large()
    {
        var ok = _schema.TryParse((ReadOnlySpan<byte>)_largePayload, out var result);
        result.Dispose();
        return ok;
    }

    // ── System.Text.Json baseline ──

    [Benchmark(Baseline = true)]
    public void StjDeserialize_Small()
    {
        using var doc = JsonDocument.Parse(_smallPayload);
    }

    [Benchmark]
    public void StjDeserialize_Medium()
    {
        using var doc = JsonDocument.Parse(_mediumPayload);
    }

    [Benchmark]
    public void StjDeserialize_Large()
    {
        using var doc = JsonDocument.Parse(_largePayload);
    }
}
