# Gluey.Contract.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Gluey.Contract.AspNetCore.svg)](https://www.nuget.org/packages/Gluey.Contract.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/Gluey.Contract.AspNetCore.svg)](https://www.nuget.org/packages/Gluey.Contract.AspNetCore)

ASP.NET Core integration for [Gluey.Contract](https://github.com/rebels-software/gluey-contracts) — schema-driven request validation with RFC 7807 error responses.

## Installation

```sh
dotnet add package Gluey.Contract.AspNetCore
dotnet add package Gluey.Contract.Json  # or Gluey.Contract.Protobuf, etc.
```

`Gluey.Contract.AspNetCore` depends only on `Gluey.Contract` (core). You choose the format package separately — JSON Schema, Protobuf, or any other format that implements `IContractSchema`.

## Quick start

### 1. Register services and schemas

```csharp
// Load schemas using your format package
var orderSchema = JsonContractSchema.Load(orderSchemaJson)!;

builder.Services.AddGlueyContracts(registry =>
{
    registry.Add("create-order", orderSchema);
});
```

The registry accepts any `IContractSchema` — JSON, Protobuf, or your own format.

### 2. Add validation to endpoints

There are two ways to wire up validation. Choose one per endpoint.

**Option A: `ContractBody` parameter** (recommended)

```csharp
app.MapPost("/orders", [Contract("create-order")] (ContractBody body) =>
{
    var name = body["name"].GetString();
    var qty = body["quantity"].GetInt32();
    return Results.Ok(new { name, qty });
}).WithContract();
```

**Option B: Filter with `HttpContext`**

```csharp
app.MapPost("/orders", (HttpContext ctx) =>
{
    var body = ctx.GetContractBody();
    var name = body["name"].GetString();
    return Results.Ok(new { name });
}).WithContractValidation("create-order");
```

Both options short-circuit with a 400 RFC 7807 response before the handler runs if validation fails.

### 3. Automatic RFC 7807 error responses

Invalid request body:
```json
{"name": "Widget", "quantity": 200}
```

Response (`400 Bad Request`):
```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Validation failed",
    "status": 400,
    "errors": [
        {
            "path": "/quantity",
            "code": "INVALID_QUANTITY",
            "message": "Quantity must be between 1 and 100"
        }
    ]
}
```

Error codes and messages come from `x-error` in the schema — no code-level error mapping needed.

## Validation approaches

### Option A: `ContractBody` parameter binding

The cleanest approach. `ContractBody` handles everything — body reading, schema resolution, validation, error response, and disposal.

```csharp
app.MapPost("/orders", [Contract("create-order")] (ContractBody body) =>
{
    var name = body["name"].GetString();
    return Results.Ok(new { name });
}).WithContract();
```

**Requirements:**
- `[Contract("schema-name")]` attribute on the handler — tells `ContractBody` which schema to validate against
- `.WithContract()` on the endpoint — registers the filter that short-circuits on validation failure
- Schema must be registered in `ContractSchemaRegistry` via `AddGlueyContracts`

**How it works:**
1. `ContractBody.BindAsync` runs during parameter binding (before filters)
2. Reads the request body and resolves the schema from the `[Contract]` attribute
3. Parses and validates the body against the schema
4. On failure: stores the error, the `ContractBodyValidationFilter` short-circuits with 400
5. On success: handler receives a valid `ContractBody`
6. `ContractBody` is auto-disposed at the end of the request via `RegisterForDispose`

**`ContractBody` API:**
- `body["name"]` — get property by name (returns `ParsedProperty`)
- `body[0]` — get property by ordinal
- `body.IsValid` — always `true` inside the handler (failures are short-circuited)
- `body.Errors` — validation errors (empty when valid)
- `body.Result` — the underlying `ParseResult` for advanced use
- `foreach (var prop in body)` — enumerate all properties with values

### Option B: Filter-based with `HttpContext`

Use this when you need `HttpContext` in the handler signature, or want to pass a compiled schema directly without the registry.

```csharp
// With registry (by name)
app.MapPost("/orders", (HttpContext ctx) =>
{
    var body = ctx.GetContractBody();
    return Results.Ok(new { name = body["name"].GetString() });
}).WithContractValidation("create-order");

// With compiled schema (inline)
var schema = JsonContractSchema.Load(schemaJson)!;
app.MapPost("/orders", (HttpContext ctx) =>
{
    var body = ctx.GetContractBody();
    return Results.Ok(new { name = body["name"].GetString() });
}).WithContractValidation(schema);
```

**Requirements:**
- `.WithContractValidation(schema)` or `.WithContractValidation("schema-name")` on the endpoint
- If using a name, schema must be registered in `ContractSchemaRegistry`
- No `[Contract]` attribute needed

**How it works:**
1. `ContractValidationFilter` runs as an endpoint filter (after parameter binding)
2. Reads the request body, validates against the schema
3. On failure: short-circuits with a 400 RFC 7807 response
4. On success: stores validated data in `HttpContext.Items`
5. Handler calls `ctx.GetContractBody()` or `ctx.GetContractResult()` to access the data

**`HttpContext` extension methods:**
- `ctx.GetContractBody()` — returns `ContractBody`, auto-disposed at end of request
- `ctx.GetContractResult()` — returns `ParseResult`, caller must dispose with `using`

Both throw `InvalidOperationException` if called outside a validated endpoint.

### Choosing between Option A and Option B

| | Option A (`ContractBody`) | Option B (Filter) |
|---|---|---|
| Schema source | `[Contract]` attribute + registry | `.WithContractValidation()` |
| Handler signature | `(ContractBody body) =>` | `(HttpContext ctx) =>` |
| Disposal | Automatic | `GetContractBody()` = automatic, `GetContractResult()` = manual |
| Inline schema | No (must use registry) | Yes (pass compiled schema) |
| Multiple formats | Yes (registry accepts `IContractSchema`) | Yes |
| Best for | Clean handlers, attribute-based routing | Inline schemas, `HttpContext` access |

## Customizing error responses

### Per-error transformation

Replace the shape of individual errors in the RFC 7807 response:

```csharp
builder.Services.AddGlueyContracts(options =>
{
    options.TransformError = (error, ctx) => new
    {
        field = error.Path,
        reason = error.Message
    };
});
```

Return `null` from the transformer to fall through to the default error shape.

### Full response override

Replace the entire error response — status code, body, everything:

```csharp
builder.Services.AddGlueyContracts(options =>
{
    options.OnValidationFailed = async (errors, ctx) =>
    {
        ctx.Response.StatusCode = 422;
        await ctx.Response.WriteAsJsonAsync(new
        {
            errorCode = "VALIDATION_FAILED",
            count = errors.Count
        });
    };
});
```

### Per-endpoint overrides

Override error transformation for a specific endpoint:

```csharp
app.MapPost("/orders", handler)
    .WithContractValidation(schema)
    .WithContractErrors((error, ctx) => new { field = error.Path });
```

## Format-agnostic design

This package doesn't depend on `Gluey.Contract.Json`. It works with any `IContractSchema` implementation:

```
App
 ├── Gluey.Contract.AspNetCore  →  Gluey.Contract (core)
 ├── Gluey.Contract.Json        →  Gluey.Contract (core)
 └── (or Gluey.Contract.Protobuf, etc.)
```

The `ContractSchemaRegistry` accepts any `IContractSchema`. Your handler receives a `ContractBody` regardless of whether the bytes were JSON, Protobuf, or any other format. The same endpoint validation middleware works for all formats.

## License

[Apache 2.0](https://github.com/rebels-software/gluey-contracts/blob/main/LICENSE)
