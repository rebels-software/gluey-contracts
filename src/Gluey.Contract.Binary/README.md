# Gluey.Contract.Binary

[![NuGet](https://img.shields.io/nuget/v/Gluey.Contract.Binary.svg)](https://www.nuget.org/packages/Gluey.Contract.Binary)
[![Downloads](https://img.shields.io/nuget/dt/Gluey.Contract.Binary.svg)](https://www.nuget.org/packages/Gluey.Contract.Binary)

Zero-allocation, schema-driven binary protocol parser for .NET. Parses custom binary payloads in a single pass against a JSON contract definition.

Part of the [Gluey.Contract](https://github.com/rebels-software/gluey-contract) library.

## Installation

```sh
dotnet add package Gluey.Contract.Binary
```

This automatically includes `Gluey.Contract` (core) as a dependency.

## Quick start

### Define a contract

```json
{
  "kind": "binary",
  "id": "sensor/telemetry",
  "name": "telemetry",
  "version": "1.0.0",
  "endianness": "little",
  "fields": {
    "recordedAgo": {
      "type": "uint16",
      "size": 2,
      "validation": { "min": 0, "max": 3600 }
    },
    "deviceId": {
      "dependsOn": "recordedAgo",
      "type": "string",
      "size": 8,
      "encoding": "ascii"
    },
    "status": {
      "dependsOn": "deviceId",
      "type": "enum",
      "size": 1,
      "values": { "0": "idle", "1": "active", "2": "error" }
    }
  }
}
```

### Parse

```csharp
var schema = BinaryContractSchema.Load(contractJson);
var result = schema!.Parse(payloadBytes);

if (result is { } parsed && parsed.IsValid)
{
    parsed["recordedAgo"].GetUInt16();   // e.g. 120
    parsed["deviceId"].GetString();       // e.g. "SENS0042"
    parsed["status"].GetString();         // e.g. "active"
}
```

## Features

- **Zero allocation** -- `ParsedProperty` is a readonly struct. No heap objects created during parsing.
- **Single pass** -- validation and indexing happen in one traversal of the byte buffer.
- **Contract-driven** -- a JSON contract defines the binary layout, field types, and validation rules.
- **Endianness support** -- big-endian and little-endian at the contract level with per-field overrides.

## Supported field types

| Type | Description |
|------|-------------|
| `uint8`, `uint16`, `uint32` | Unsigned integers |
| `int8`, `int16`, `int32` | Signed integers |
| `float32`, `float64` | Floating point |
| `boolean` | Boolean (0 = false, non-zero = true) |
| `string` | Fixed-length ASCII or UTF-8 |
| `enum` | Mapped value sets |
| `bits` | Bit field containers with sub-fields |
| `struct` (in arrays) | Composite types |
| `padding` | Skip bytes |

## License

[Apache 2.0](https://github.com/rebels-software/gluey-contract/blob/main/LICENSE)
