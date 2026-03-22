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

## Contract reference

### Contract envelope

```json
{
  "kind": "binary",
  "id": "sensor/telemetry",
  "name": "telemetry",
  "version": "1.0.0",
  "endianness": "little",
  "fields": { }
}
```

- `endianness` — `"little"` (default) or `"big"`, applies to all numeric fields
- Fields form a dependency chain via `dependsOn` — no absolute offsets

### Scalar types

```json
"temperature":  { "type": "uint8",   "size": 1 }
"rpm":          { "type": "uint16",  "size": 2 }
"counter":      { "type": "uint32",  "size": 4 }
"offset":       { "type": "int8",    "size": 1 }
"altitude":     { "type": "int16",   "size": 2 }
"pressure":     { "type": "int32",   "size": 4 }
"voltage":      { "type": "float32", "size": 4 }
"latitude":     { "type": "float64", "size": 8 }
"active":       { "type": "boolean", "size": 1 }
```

**Truncated numerics** — `size` smaller than natural width, sign/zero-extended on read:

```json
"sensor":  { "type": "int32",  "size": 3 }
"bigId":   { "type": "uint32", "size": 3 }
```

**Per-field endianness override:**

```json
"timestamp": { "type": "uint16", "size": 2, "endianness": "big" }
```

### String

```json
"deviceId": { "type": "string", "size": 8, "encoding": "ascii" }
"label":    { "type": "string", "size": 16, "encoding": "utf-8", "mode": "trim" }
```

| Mode | Behavior |
|------|----------|
| `plain` | No trimming |
| `trimStart` | Remove leading null bytes |
| `trimEnd` | Remove trailing null bytes (default) |
| `trim` | Remove both |

### Enum

```json
"status": {
  "type": "enum", "size": 1,
  "values": { "0": "idle", "1": "active", "2": "error" }
}
```

Dual access: `parsed["status"].GetUInt8()` → `1`, `parsed["statuss"].GetString()` → `"active"`

### Bits

```json
"flags": {
  "type": "bits", "size": 2,
  "fields": {
    "isCharging": { "bit": 0, "bits": 1, "type": "boolean" },
    "errorCode":  { "bit": 1, "bits": 4, "type": "uint8" },
    "priority":   { "bit": 5, "bits": 3, "type": "uint8" }
  }
}
```

Access: `parsed["flags/isCharging"].GetBoolean()`, `parsed["flags/errorCode"].GetUInt8()`

Container sizes up to 2 bytes (16 bits).

### Array — fixed count

```json
"voltages": {
  "type": "array", "count": 3,
  "element": { "type": "float32", "size": 4 }
}
```

Access: `parsed["voltages/0"].GetSingle()`, `parsed["voltages/2"].GetSingle()`

### Array — semi-dynamic count

```json
"errorCount": { "type": "uint8", "size": 1 },
"errors": {
  "dependsOn": "errorCount",
  "type": "array", "count": "errorCount",
  "element": { "type": "uint16", "size": 2 }
}
```

Count resolved at parse time from the referenced field.

### Struct (array element)

```json
"records": {
  "type": "array", "count": "recordCount",
  "element": {
    "type": "struct", "size": 5,
    "fields": {
      "code":      { "type": "uint16", "size": 2 },
      "severity":  { "dependsOn": "code", "type": "uint8", "size": 1 },
      "timestamp": { "dependsOn": "severity", "type": "uint16", "size": 2, "endianness": "big" }
    }
  }
}
```

Access: `parsed["records/0/code"].GetUInt16()`, `parsed["records/1/severity"].GetUInt8()`

Struct sub-fields have their own dependency chain.

### Padding

```json
"_reserved": { "type": "padding", "size": 4 }
```

Skips bytes. Not exposed in parse result.

### Validation

```json
"level":    { "type": "uint8",  "size": 1, "validation": { "min": 0, "max": 100 } }
"temp":     { "type": "int16",  "size": 2, "validation": { "min": -40, "max": 85 } }
"badgeId":  { "type": "string", "size": 8, "encoding": "ascii",
              "validation": { "pattern": "^[A-Z0-9]+$", "minLength": 3, "maxLength": 8 } }
```

| Rule | Applies to | Description |
|------|-----------|-------------|
| `min` / `max` | numeric, float | Inclusive value range |
| `pattern` | string | Regex match |
| `minLength` / `maxLength` | string | Length after trimming |

All validation errors are collected (not fail-fast). Invalid values remain accessible.

### Custom error messages with `x-error`

Any field with validation can carry an `x-error` object. When validation fails, the error is enriched with your custom metadata:

```json
"level": {
  "type": "uint8", "size": 1,
  "validation": { "min": 0, "max": 100 },
  "x-error": {
    "code": "INVALID_LEVEL",
    "title": "Invalid battery level",
    "detail": "Battery level must be between 0 and 100",
    "type": "https://api.example.com/errors/invalid-level"
  }
}
```

```csharp
if (!parsed.IsValid)
{
    var error = parsed.Errors[0];
    // error.Code      → ValidationErrorCode.MaximumExceeded
    // error.Message   → "Battery level must be between 0 and 100"  (from x-error.detail)
    // error.ErrorInfo → SchemaErrorInfo { Code: "INVALID_LEVEL", Title: "...", ... }
}
```

| Field | Maps to | Purpose |
|-------|---------|---------|
| `code` | `SchemaErrorInfo.Code` | Machine-readable domain error code |
| `title` | `SchemaErrorInfo.Title` | Short summary |
| `detail` | `SchemaErrorInfo.Detail` / `ValidationError.Message` | Replaces default message |
| `type` | `SchemaErrorInfo.Type` | URI identifying the error type (RFC 7807) |

All fields are optional. When `detail` is present it replaces the default `Message`. The original `ValidationErrorCode` is always preserved.

### Dependency chain

```json
"fields": {
  "first":  { "type": "uint8", "size": 1 },
  "second": { "dependsOn": "first", "type": "uint16", "size": 2 },
  "third":  { "dependsOn": "second", "type": "string", "size": 8, "encoding": "ascii" }
}
```

- Exactly one root field (no `dependsOn`) — starts at byte 0
- Each field starts where its parent ends
- No cycles, no shared parents

## License

[Apache 2.0](https://github.com/rebels-software/gluey-contract/blob/main/LICENSE)
