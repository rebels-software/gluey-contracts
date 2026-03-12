# ADR 14: ValidationError Keeps String Path

## Status
Accepted

## Context
`ValidationError` is a readonly struct carrying three fields: `string Path` (RFC 6901 JSON Pointer), `ValidationErrorCode Code`, and `string Message`. We evaluated replacing `string Path` with an `int PathIndex` into a schema-owned path table, which would shrink the struct from ~24 bytes to ~8 bytes and remove all managed references, making it fully blittable.

## Decision
Keep `ValidationError` with `string Path` and `string Message` as direct fields.

## Rationale

### No allocation benefit
The path strings are pre-computed from the schema structure and owned by the schema's path table. The message strings are static constants in `ValidationErrorMessages`. Constructing a `ValidationError` copies references to these existing strings — it does not allocate. The struct is already allocation-free.

### Errors are the unhappy path
Validation errors are only produced when input fails validation. The hot path — successful validation — never touches `ValidationError` or `ErrorCollector` contents. Shrinking the struct size has no measurable impact on the performance-critical path.

### Ergonomics matter
With `string Path`, errors are self-contained and inspectable without needing access to the schema or `ParseResult`:

```csharp
foreach (var error in result.Errors)
{
    Console.WriteLine($"{error.Path}: {error.Message}");
}
```

An index-based approach would require the consumer to resolve paths through a secondary lookup, adding friction for no practical gain.

## Consequences
- `ValidationError` remains a simple, self-describing value type.
- Consumers can inspect errors without retaining a reference to the schema or parse result.
- The struct is larger (~24 bytes vs ~8 bytes) but this has no impact on hot-path performance.
