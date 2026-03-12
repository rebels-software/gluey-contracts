# Usage Guide

## Schema and Sample Data

Throughout this guide we use the following schema and JSON payload.

### Schema

```json
{
  "type": "object",
  "required": ["id", "name", "email"],
  "properties": {
    "id": { "type": "integer" },
    "name": { "type": "string", "minLength": 1 },
    "email": { "type": "string", "format": "email" },
    "age": { "type": "integer", "minimum": 0, "maximum": 150 },
    "score": { "type": "number" },
    "active": { "type": "boolean" },
    "address": {
      "type": "object",
      "properties": {
        "street": { "type": "string" },
        "city": { "type": "string" },
        "zip": { "type": "string", "pattern": "^[0-9]{5}$" }
      }
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" },
      "minItems": 1,
      "uniqueItems": true
    },
    "orders": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "product": { "type": "string" },
          "quantity": { "type": "integer" },
          "price": { "type": "number" }
        }
      }
    }
  }
}
```

### JSON Payload

```json
{
  "id": 42,
  "name": "Alice",
  "email": "alice@example.com",
  "age": 30,
  "score": 97.5,
  "active": true,
  "address": {
    "street": "123 Main St",
    "city": "Springfield",
    "zip": "62701"
  },
  "tags": ["admin", "editor"],
  "orders": [
    { "product": "Widget", "quantity": 3, "price": 9.99 },
    { "product": "Gadget", "quantity": 1, "price": 24.50 }
  ]
}
```

## Loading a Schema

```csharp
using Gluey.Contract;
using Gluey.Contract.Json;
using System.Text;

// From a string
var schema = JsonContractSchema.Load(schemaJson);

// From UTF-8 bytes (preferred — avoids string-to-bytes conversion)
byte[] schemaBytes = File.ReadAllBytes("schema.json");
var schema = JsonContractSchema.Load(schemaBytes.AsSpan());

// With TryLoad for explicit success/failure handling
if (JsonContractSchema.TryLoad(schemaJson, out var schema))
{
    // schema is ready
}
```

## Parsing and Validating

```csharp
byte[] data = Encoding.UTF8.GetBytes(jsonPayload);

// Parse with property access (byte[] overload populates OffsetTable)
if (schema.TryParse(data, out var result))
{
    // result.IsValid == true, access properties via result[...]
}

// Parse for validation only (ReadOnlySpan overload — no OffsetTable)
ReadOnlySpan<byte> span = data.AsSpan();
if (schema.TryParse(span, out var validationResult))
{
    // validationResult.IsValid == true, but indexers return ParsedProperty.Empty
}
```

**Important:** Always dispose the result to return pooled buffers.

```csharp
using var result = schema.Parse(data);
// or
schema.TryParse(data, out var result);
try
{
    // use result
}
finally
{
    result.Dispose();
}
```

## Accessing Properties

### Direct access by path

Every schema-known property has a precomputed RFC 6901 JSON Pointer path.
Access nested properties in O(1) without chaining.

```csharp
schema.TryParse(data, out var result);

result["/id"].GetInt32()          // 42
result["/name"].GetString()       // "Alice"
result["/email"].GetString()      // "alice@example.com"
result["/age"].GetInt32()         // 30
result["/score"].GetDouble()      // 97.5
result["/active"].GetBoolean()    // true

// Deep nested — single lookup, no chaining required
result["/address/street"].GetString()  // "123 Main St"
result["/address/city"].GetString()    // "Springfield"
result["/address/zip"].GetString()     // "62701"
```

### Hierarchical (chained) access

Navigate from parent to child using the string indexer on `ParsedProperty`.

```csharp
var address = result["/address"];
address["street"].GetString()     // "123 Main St"
address["city"].GetString()       // "Springfield"
```

### Shorthand without leading slash

The string indexer accepts paths with or without the leading `/`.

```csharp
result["name"].GetString()            // "Alice"
result["address/street"].GetString()  // "123 Main St"
```

### Array element access by index

```csharp
var tags = result["/tags"];
tags[0].GetString()               // "admin"
tags[1].GetString()               // "editor"
tags.Count                        // 2

var firstOrder = result["/orders"][0];
firstOrder["product"].GetString() // "Widget"
firstOrder["quantity"].GetInt32() // 3
firstOrder["price"].GetDouble()   // 9.99
```

### Iterating array elements

`ParsedProperty` supports `foreach` via a zero-allocation duck-typed enumerator.

```csharp
foreach (var tag in result["/tags"])
{
    Console.WriteLine(tag.GetString());
}
// Output:
// admin
// editor

foreach (var order in result["/orders"])
{
    var product = order["product"].GetString();
    var qty = order["quantity"].GetInt32();
    Console.WriteLine($"{product} x{qty}");
}
// Output:
// Widget x3
// Gadget x1
```

### Iterating all top-level properties

`ParseResult` supports `foreach` over all properties that have values.

```csharp
foreach (var prop in result)
{
    Console.WriteLine($"{prop.Path} = {prop.GetString()}");
}
```

### Checking if a property exists

```csharp
if (result["/age"].HasValue)
{
    int age = result["/age"].GetInt32();
}

// Missing or unknown properties return ParsedProperty.Empty
result["/nonexistent"].HasValue   // false
result["/nonexistent"].GetString() // ""
```

## Type Coercion Behavior

`ParsedProperty` works on raw UTF-8 bytes. The `Get*` methods attempt to parse
whatever bytes are stored — there is no runtime type tag. This means:

### Calling GetInt32 on a string value

The bytes contain `"Alice"` (a JSON string). `Utf8Parser.TryParse` fails to
parse it as an integer and returns `default(int)`.

```csharp
result["/name"].GetInt32()        // 0
```

### Calling GetString on a numeric value

The bytes contain `42`. `Encoding.UTF8.GetString` decodes the raw bytes,
returning the number as its text representation.

```csharp
result["/id"].GetString()         // "42"
```

### Calling GetDouble on an integer value

`Utf8Parser.TryParse` handles this — integers are valid doubles.

```csharp
result["/id"].GetDouble()         // 42.0
```

### Calling GetBoolean on a non-boolean value

Returns `true` only if the raw bytes are exactly `true` (4 bytes starting with `t`).
Everything else returns `false`.

```csharp
result["/name"].GetBoolean()      // false
result["/id"].GetBoolean()        // false
result["/active"].GetBoolean()    // true
```

### Calling GetInt32 on a decimal number

`Utf8Parser.TryParse` for `int` fails on `97.5` (not an integer).

```csharp
result["/score"].GetInt32()       // 0
```

### Calling GetDecimal on a number

Works for both integers and decimals.

```csharp
result["/score"].GetDecimal()     // 97.5
result["/id"].GetDecimal()        // 42
```

### Missing property

All `Get*` methods return their type's default when `HasValue` is false.

```csharp
result["/missing"].GetString()    // ""
result["/missing"].GetInt32()     // 0
result["/missing"].GetDouble()    // 0.0
result["/missing"].GetBoolean()   // false
result["/missing"].GetDecimal()   // 0
```

### Summary table

| Actual JSON value | GetString | GetInt32 | GetInt64 | GetDouble | GetDecimal | GetBoolean |
|---|---|---|---|---|---|---|
| `42` | `"42"` | `42` | `42` | `42.0` | `42` | `false` |
| `97.5` | `"97.5"` | `0` | `0` | `97.5` | `97.5` | `false` |
| `"Alice"` | `"Alice"` | `0` | `0` | `0.0` | `0` | `false` |
| `true` | `"true"` | `0` | `0` | `0.0` | `0` | `true` |
| `false` | `"false"` | `0` | `0` | `0.0` | `0` | `false` |
| missing | `""` | `0` | `0` | `0.0` | `0` | `false` |

**Note:** There is no `GetDateTime` or `GetGuid` method. For format-validated
strings (email, date-time, uuid, etc.), use `GetString()` and parse on the
consumer side. Schema-level format validation (via `SchemaOptions.AssertFormat`)
ensures the string conforms to the expected format before you access it.

## Handling Validation Errors

```csharp
byte[] badData = Encoding.UTF8.GetBytes("""{"id": "not-a-number"}""");
schema.TryParse(badData, out var result);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"[{error.Code}] {error.Path}: {error.Message}");
    }
}
// Output:
// [TypeMismatch] /id: Value does not match the expected type.
// [RequiredMissing] /name: A required property is missing.
// [RequiredMissing] /email: A required property is missing.
```

Errors are self-contained — each carries the full JSON Pointer path, a
machine-readable `ValidationErrorCode`, and a human-readable message.

### Error collection limits

`ErrorCollector` has a configurable capacity (default 64). When the limit is
reached, the last slot is replaced with a `TooManyErrors` sentinel and further
errors are silently dropped. This prevents runaway error accumulation on
deeply invalid payloads.

## Raw Byte Access

For advanced use cases, access the underlying UTF-8 bytes directly
without materialization.

```csharp
ReadOnlySpan<byte> raw = result["/name"].RawBytes;
// raw contains the UTF-8 bytes of "Alice" (without JSON quotes)
```
