# Gluey.Contract

[![Build](https://github.com/rebels-software/gluey-contracts/actions/workflows/main.yml/badge.svg)](https://github.com/rebels-software/gluey-contracts/actions/workflows/main.yml)

[![codecov](https://codecov.io/gh/rebels-software/gluey-contracts/graph/badge.svg)](https://codecov.io/gh/rebels-software/gluey-contracts)

[![NuGet](https://img.shields.io/nuget/v/Gluey.Contract.svg)](https://www.nuget.org/packages/Gluey.Contract)

![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)

## Overview

**Gluey.Contract** is a high-performance, zero-allocation .NET library for schema-driven validation and access of serialized data. Instead of deserializing bytes into objects, it validates and indexes the raw byte buffer in a single pass — giving you direct access to values with full path tracking.

Think of it as **FlatBuffers philosophy applied to JSON** (and other formats): no deserialization, no object allocation, just a schema-aware index into the original bytes.

## Why

Traditional request processing in .NET:

```
JSON bytes → Deserialize to objects → Validate → Map errors to ProblemDetails
                  ↑                       ↑                    ↑
           allocates everything     second pass          loses JSON paths
```

With Gluey.Contract:

```
JSON bytes → Single pass: validate + index → access values on demand
                       ↑                              ↑
              zero allocation, schema-aware    exact JSON pointer paths
```

### What you get

- **Zero allocation** — no objects created during parsing. Values read from the byte buffer on demand.
- **Single-pass validation** — schema validation happens during parsing, not as a separate step.
- **Exact JSON Pointer paths** — validation errors include [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) paths like `/devices/0/serialNumber`, not `Devices[0].SerialNumber`.
- **Format-agnostic interface** — same API whether the bytes came from JSON, Protobuf, or any other wire format.
- **UTF-8 native** — operates directly on UTF-8 bytes (what Kestrel, HTTP, and file I/O deliver). ASCII works as-is (subset of UTF-8). UTF-16/UTF-32 input must be transcoded to UTF-8 first — the library does not perform encoding conversion internally, keeping the hot path allocation-free.

## Packages

| Package | Description |
|---------|-------------|
| `Gluey.Contract` | Core: schema model, parsed data interface, offset table, validation primitives |
| `Gluey.Contract.Json` | JSON byte parser — validates and indexes JSON against a JSON Schema |

### Planned

| Package | Description |
|---------|-------------|
| `Gluey.Contract.AspNetCore` | ASP.NET Core integration — model binder, ProblemDetails with JSON pointer paths |
| `Gluey.Contract.Protobuf` | Protobuf wire format parser |
| `Gluey.Contract.Postgres` | PostgreSQL wire protocol reader |
| `Gluey.Contract.Redis` | Redis RESP protocol reader |

## How it works

### 1. Define a schema

For JSON, use standard JSON Schema:

```json
{
  "type": "object",
  "properties": {
    "serialNumber": { "type": "string", "maxLength": 64 },
    "csr": { "type": "string" },
    "device": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "tags": { "type": "array", "items": { "type": "string" } }
      }
    }
  },
  "required": ["serialNumber", "csr"]
}
```

### 2. Parse raw bytes — single pass, zero allocation

Two API styles — choose what fits your codebase:

#### TryParse — .NET-idiomatic

```csharp
var schema = JsonContractSchema.Load(schemaJson);

if (schema.TryParse(requestBytes, out var result))
{
    result["serialNumber"].GetString();        // reads from byte buffer
    result["serialNumber"].Path;               // "/serialNumber"
    result["device"]["name"].GetString();      // nested access
    result["device"]["tags"][0].GetString();   // array access
    result["device"]["tags"][0].Path;          // "/device/tags/0"
    result.Dispose();
}
else
{
    // result.Errors → [{ Path: "/csr", Code: "RequiredMissing" }]
    result.Dispose();
}
```

#### Parse — returns nullable

```csharp
var result = schema.Parse(requestBytes);

if (result is { } parsed)
{
    if (!parsed.Errors.HasErrors)
    {
        parsed["serialNumber"].GetString();
        parsed["device"]["tags"][0].Path;  // "/device/tags/0"
    }
    parsed.Dispose();
}
// null → structurally invalid JSON
```

Both share the same internal parsing logic. No objects were allocated. `ParsedProperty` is a `readonly struct` holding an offset and length into the original byte buffer. Values are materialized only when you call `GetString()`, `GetInt32()`, etc. `Dispose()` returns pooled buffers.

### 4. Validation errors with [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) JSON Pointer paths

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "errors": [
    { "path": "/csr", "code": "REQUIRED", "message": "CSR is required" },
    { "path": "/devices/0/serialNumber", "code": "MAX_LENGTH", "message": "Must be at most 64 characters" },
    { "path": "/devices/2/tags/1", "code": "PATTERN", "message": "Must match ^[a-z-]+$" }
  ]
}
```

## Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 9.0. Flat object schema with 5 typed properties + `additionalProperties` + `required`.

Three comparison points:
- **Gluey TryParse** — single-pass: validate against schema + build offset table for property access.
- **Gluey ValidateOnly** — single-pass: validate against schema only, no offset table.
- **STJ + JsonSchema.Net** — two-pass: `JsonDocument.Parse()` then `JsonSchema.Evaluate()`. The standard .NET approach to JSON Schema validation.
- **STJ Parse Only** — `JsonDocument.Parse()` alone (no validation). Baseline reference.

| Method | Payload | Mean | Allocated |
|--------|---------|-----:|----------:|
| **Gluey TryParse** | Small (100B) | 876 ns | **0 B** |
| **Gluey TryParse** | Medium (5KB) | 17,909 ns | **0 B** |
| **Gluey TryParse** | Large (50KB) | 161,435 ns | **1 B** |
| **Gluey ValidateOnly** | Small (100B) | 699 ns | **0 B** |
| **Gluey ValidateOnly** | Medium (5KB) | 18,375 ns | **0 B** |
| **Gluey ValidateOnly** | Large (50KB) | 166,007 ns | **1 B** |
| STJ + JsonSchema.Net | Small (100B) | 6,555 ns | 6,745 B |
| STJ + JsonSchema.Net | Medium (5KB) | 91,079 ns | 156,759 B |
| STJ + JsonSchema.Net | Large (50KB) | 843,928 ns | 1,414,489 B |
| STJ Parse Only | Small (100B) | 312 ns | 72 B |
| STJ Parse Only | Medium (5KB) | 7,504 ns | 72 B |
| STJ Parse Only | Large (50KB) | 69,138 ns | 80 B |

### Key takeaways

- **5-7x faster** than STJ + JsonSchema.Net for schema validation.
- **Zero heap allocation** — no GC pressure at any payload size. STJ + JsonSchema.Net allocates up to 1.4 MB.
- **Single pass** — Gluey validates and indexes in one pass over the bytes. The two-pass approach (parse then validate) pays tokenization cost twice.
- The 1B on large payloads is BenchmarkDotNet measurement noise.

> Run benchmarks yourself: `dotnet run --project benchmarks/Gluey.Contract.Benchmarks -c Release`

## Architecture

```
Gluey.Contract (core)
  │
  ├── Schema model         — describes expected structure (types, constraints, paths)
  ├── ParsedProperty       — struct: offset + length into byte buffer
  ├── Offset table         — maps property names to byte positions
  ├── ParseResult          — parsed properties + validation errors
  └── Validation errors    — code + path + message
        │
        ├── Gluey.Contract.Json       — JSON byte parser using JSON Schema
        ├── Gluey.Contract.Protobuf   — Protobuf parser (planned)
        └── Gluey.Contract.Postgres   — PG wire protocol reader (planned)
```

Each format package implements byte-reading against its wire format. The consuming code uses the same `ParsedProperty` interface regardless of format.

## Part of the Gluey ecosystem

Gluey.Contract is the **runtime validation engine** for the [Gluey](https://github.com/rebels-software/gluey) ecosystem. When used with the Gluey DSL, schemas are generated automatically from system definitions — covering HTTP payloads, database schemas, message broker topics, and wire protocols from a single source of truth.

Gluey.Contract also works standalone — any .NET developer can use it with standard JSON Schema files without the DSL.

## Getting Started

### Prerequisites
- [.NET SDK 9.0+](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

### Installation

```sh
dotnet add package Gluey.Contract.Json
```

## Contributing

We welcome contributions! Please follow these steps:
1. Fork this repository.
2. Create a new branch (`git checkout -b feature-name`).
3. Commit your changes (`git commit -m "Add feature"`).
4. Push to your branch (`git push origin feature-name`).
5. Open a Pull Request.

Ensure code follows the .NET coding standards:
- Use `dotnet format` to auto-format code.
- Run `dotnet test` before submitting a PR.

## License

This project is licensed under the [Apache 2.0 License](LICENSE).

## Contact

For questions or support, open an issue or contact us at [we@rebels.software](mailto:we@rebels.software).
