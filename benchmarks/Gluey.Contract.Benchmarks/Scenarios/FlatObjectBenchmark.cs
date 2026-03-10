using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Json.Schema;
using Gluey.Contract.Json;
using Gluey.Contract.Benchmarks.Payloads;

namespace Gluey.Contract.Benchmarks.Scenarios;

/// <summary>
/// Benchmarks TryParse and validate-only paths against a flat JSON object schema
/// with string, integer, boolean, number, and format-annotated properties.
/// Includes System.Text.Json baseline for comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class FlatObjectBenchmark
{
    private JsonContractSchema _schema = null!;
    private JsonSchema _jsonSchema = null!;
    private byte[] _smallPayload = null!;
    private byte[] _mediumPayload = null!;
    private byte[] _largePayload = null!;

    private const string SchemaJson = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" },
                "active": { "type": "boolean" },
                "score": { "type": "number" },
                "email": { "type": "string", "format": "email" }
            },
            "required": ["name", "age", "active", "score", "email"],
            "additionalProperties": { "type": ["string", "integer"] }
        }
        """;

    [GlobalSetup]
    public void Setup()
    {
        _schema = JsonContractSchema.Load(SchemaJson)
            ?? throw new InvalidOperationException("Failed to load flat object schema");
        _jsonSchema = JsonSchema.FromText(SchemaJson);

        _smallPayload = PayloadGenerator.GenerateFlat(100);
        _mediumPayload = PayloadGenerator.GenerateFlat(5_000);
        _largePayload = PayloadGenerator.GenerateFlat(50_000);
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

    // ── System.Text.Json baseline (parse only, no validation) ──

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

    // ── JsonSchema.Net baseline (parse + validate -- two-pass approach) ──

    [Benchmark]
    public EvaluationResults StjValidate_Small()
    {
        using var doc = JsonDocument.Parse(_smallPayload);
        return _jsonSchema.Evaluate(doc.RootElement);
    }

    [Benchmark]
    public EvaluationResults StjValidate_Medium()
    {
        using var doc = JsonDocument.Parse(_mediumPayload);
        return _jsonSchema.Evaluate(doc.RootElement);
    }

    [Benchmark]
    public EvaluationResults StjValidate_Large()
    {
        using var doc = JsonDocument.Parse(_largePayload);
        return _jsonSchema.Evaluate(doc.RootElement);
    }
}
