# Gluey.Contract.Json

[![NuGet](https://img.shields.io/nuget/v/Gluey.Contract.Json.svg)](https://www.nuget.org/packages/Gluey.Contract.Json)
[![Downloads](https://img.shields.io/nuget/dt/Gluey.Contract.Json.svg)](https://www.nuget.org/packages/Gluey.Contract.Json)

Zero-allocation, schema-driven JSON parser for .NET. Validates and indexes raw JSON bytes in a single pass against a standard JSON Schema — no deserialization, no object allocation.

Part of the [Gluey.Contract](https://github.com/rebels-software/gluey-contract) library.

## Installation

```sh
dotnet add package Gluey.Contract.Json
```

This automatically includes `Gluey.Contract` (core) as a dependency.

## Quick start

### Define a JSON Schema

```json
{
  "type": "object",
  "properties": {
    "serialNumber": { "type": "string", "maxLength": 64 },
    "csr": { "type": "string" }
  },
  "required": ["serialNumber", "csr"]
}
```

### TryParse

```csharp
var schema = JsonContractSchema.FromFile("enrollment.schema.json");

if (schema.TryParse(requestBytes, out ParsedData data, out ValidationError[] errors))
{
    data["serialNumber"].GetString();   // reads from byte buffer on demand
    data["serialNumber"].Path;          // "/serialNumber"
}
else
{
    // errors → [{ Path: "/csr", Code: "REQUIRED", Message: "CSR is required" }]
}
```

### Result

```csharp
var result = schema.Parse(requestBytes);

if (result.IsSuccess)
    result.Value["serialNumber"].GetString();
```

## Features

- **Zero allocation** — `ParsedProperty` is a readonly struct. No heap objects created during parsing.
- **Single pass** — validation and indexing happen in one traversal of the byte buffer.
- **[RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) JSON Pointer paths** — validation errors include exact paths like `/devices/0/serialNumber`.
- **Standard JSON Schema** — uses the same schema format you already know.

## Supported JSON Schema keywords

| Keyword | Supported |
|---------|-----------|
| `type` | string, number, integer, boolean, object, array, null |
| `properties`, `required` | Object structure |
| `items` | Array element type |
| `minLength`, `maxLength` | String constraints |
| `minimum`, `maximum` | Numeric constraints |
| `enum` | Fixed value sets |
| `pattern` | Regex validation |
| `additionalProperties` | Extra field control |
| `format` | email, uuid, date-time (optional) |

## License

[Apache 2.0](https://github.com/rebels-software/gluey-contract/blob/main/LICENSE)
