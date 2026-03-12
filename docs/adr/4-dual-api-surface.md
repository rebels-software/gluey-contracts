# ADR 4: Dual API Surface — TryParse and Result

## Status
Accepted

## Context
Validation failures are expected, not exceptional. However, as a standalone NuGet library, Gluey.Contract should not force consumers into a specific error-handling pattern. Some teams prefer `TryParse`, others prefer `Result<T>`, and imposing one style limits adoption.

## Decision
Gluey.Contract exposes two API surfaces for parsing:

### 1. TryParse — .NET-idiomatic, zero opinion

```csharp
if (schema.TryParse(bytes, out ParsedData data, out ValidationError[] errors))
{
    data["serial"].GetString();
}
```

Familiar to every .NET developer. No custom types to learn.

### 2. Result — for consumers who prefer explicit result types

```csharp
var result = schema.Parse(bytes);

if (result.IsSuccess)
    result.Value["serial"].GetString();
```

Both methods share the same internal parsing logic. The `Result<T>` type is included in `Gluey.Contract` as a lightweight struct — not an opinionated framework, just a convenience wrapper.

### What we do NOT do

- No exceptions for validation failures. `Parse()` returns a failed `Result`, it does not throw.
- Exceptions are reserved for programming errors only (e.g., null schema, disposed buffer).

## Consequences
- Two entry points to maintain, but they share one implementation.
- Consumers choose the pattern that fits their codebase.
- `Result<T>` is a simple struct in the library, not a third-party dependency.
