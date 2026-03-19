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
builder.Services.AddGlueyContracts(registry =>
{
    registry.Add("create-order", """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "minLength": 1 },
                "quantity": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 100,
                    "x-error": {
                        "code": "INVALID_QUANTITY",
                        "detail": "Quantity must be between 1 and 100"
                    }
                }
            },
            "required": ["name", "quantity"]
        }
        """);
});
```

### 2. Add validation to endpoints

**Option A: `ContractBody` as parameter** (recommended)

```csharp
app.MapPost("/orders", [Contract("create-order")] (ContractBody body) =>
{
    var name = body["name"].GetString();
    var qty = body["quantity"].GetInt32();
    return Results.Ok(new { name, qty });
}).WithContract();
```

`ContractBody` reads, validates, and short-circuits with 400 automatically. Auto-disposed at the end of the request — no `using` needed.

**Option B: Filter-based with `HttpContext`**

```csharp
app.MapPost("/orders", (HttpContext ctx) =>
{
    var body = ctx.GetContractBody();  // auto-disposed
    var name = body["name"].GetString();
    return Results.Ok(new { name });
}).WithContractValidation("create-order");
```

Invalid requests are short-circuited with a 400 response before the handler runs.

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

## Customizing error responses

### Per-error transformation

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

### Full response override

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

```csharp
app.MapPost("/orders", handler)
    .WithContractValidation(schema)
    .WithContractErrors((error, ctx) => new { field = error.Path });
```

## Inline schema usage

You can also pass a compiled schema directly instead of using the registry:

```csharp
var schema = JsonContractSchema.Load(schemaJson)!;

app.MapPost("/orders", handler)
    .WithContractValidation(schema);
```

## License

[Apache 2.0](https://github.com/rebels-software/gluey-contracts/blob/main/LICENSE)
