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

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Json.Schema;
using Gluey.Contract.Json;
using Gluey.Contract.Benchmarks.Payloads;

namespace Gluey.Contract.Benchmarks.Scenarios;

/// <summary>
/// Benchmarks TryParse and validate-only paths against a nested JSON object schema
/// with 2-3 levels of object nesting (address with geo, contact).
/// Includes System.Text.Json baseline for comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class NestedObjectBenchmark
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
                "address": {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "city": { "type": "string" },
                        "zip": { "type": "string" },
                        "geo": {
                            "type": "object",
                            "properties": {
                                "lat": { "type": "number" },
                                "lng": { "type": "number" }
                            },
                            "required": ["lat", "lng"]
                        }
                    },
                    "required": ["street", "city", "zip"]
                },
                "contact": {
                    "type": "object",
                    "properties": {
                        "phone": { "type": "string" },
                        "email": { "type": "string" }
                    },
                    "required": ["phone", "email"]
                }
            },
            "required": ["name", "address", "contact"],
            "additionalProperties": {
                "type": "object",
                "properties": {
                    "label": { "type": "string" },
                    "value": { "type": "number" },
                    "detail": {
                        "type": "object",
                        "properties": {
                            "info": { "type": "string" }
                        }
                    }
                }
            }
        }
        """;

    [GlobalSetup]
    public void Setup()
    {
        _schema = JsonContractSchema.Load(SchemaJson)
            ?? throw new InvalidOperationException("Failed to load nested object schema");
        _jsonSchema = JsonSchema.FromText(SchemaJson);

        _smallPayload = PayloadGenerator.GenerateNested(100);
        _mediumPayload = PayloadGenerator.GenerateNested(5_000);
        _largePayload = PayloadGenerator.GenerateNested(50_000);
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
