# Coding Conventions

**Analysis Date:** 2026-03-18

## Naming Patterns

**Files:**
- PascalCase for all C# files: `SchemaNode.cs`, `ArrayValidator.cs`, `ErrorCollector.cs`
- Test files follow same pattern with `Tests` suffix: `ArrayValidatorTests.cs`, `JsonContractSchemaApiTests.cs`
- Directory names are PascalCase: `Validation/`, `Buffers/`, `Parsing/`, `Schema/`

**Functions/Methods:**
- PascalCase for all public and internal methods: `ValidateMinItems()`, `Add()`, `GetEnumerator()`, `Dispose()`
- Helper methods follow same convention: `BuildChildPath()`, `TryGetValue()`, `MoveNext()`

**Variables:**
- camelCase for local variables and parameters: `count`, `element`, `arrayOrdinal`, `collector`
- underscore prefix for private fields: `_errors`, `_capacity`, `_offsetTable`, `_count`
- UPPER_SNAKE_CASE for public constants: `DefaultCapacity`
- `t_` prefix for thread-static fields: `t_cached`

**Types:**
- PascalCase for all class and struct names: `SchemaNode`, `ParseResult`, `ErrorCollector`, `ArrayBuffer`
- PascalCase for enums and enum values: `ValidationErrorCode`, `SchemaType`
- Interface names use `I` prefix with PascalCase: (when used)
- Sealed classes marked explicitly: `internal sealed class SchemaNode`

**Namespace:**
- Root namespace: `Gluey.Contract`
- Test namespaces append `.Tests`: `Gluey.Contract.Json.Tests`, `Gluey.Contract.Tests`
- Sub-namespaces not used; organize by directory

## Code Style

**Formatting:**
- No explicit formatter config found; code uses standard C# conventions
- 4-space indentation (standard)
- Opening braces on same line (K&R style): `public void Dispose() { ... }`
- One statement per line

**Linting:**
- Language version: C# 13 (set in `.csproj` via `<LangVersion>13</LangVersion>`)
- Implicit using statements enabled: `<ImplicitUsings>enable</ImplicitUsings>`
- Nullable reference types enforced: `<Nullable>enable</Nullable>`
- No external analyzer config files found; relies on compiler defaults

**Access Modifiers:**
- Explicitly specify access level for all members (never rely on defaults)
- `internal` heavily used for implementation hiding: `internal sealed class SchemaNode`
- `internal` for test-visible types: `InternalsVisibleTo` attributes grant test assembly access
- `public` only for public API surface
- `private` for internal state and helper methods
- Property-level access specification: `internal string Path { get; }` (get-only properties common)

## Import Organization

**Order:**
1. System namespaces: `using System.Text;`, `using System.Buffers;`
2. System.Collections and extensions: `using System.Collections;`
3. Framework libraries: (rarely used)
4. Local project namespaces: `using Gluey.Contract;`
5. Test-specific: `using FluentAssertions;`, `using NUnit;`

**Path Aliases:**
- No aliases used; fully qualified names throughout
- Namespace imports are flat (no nesting)

**Global Usings:**
- Not observed in analyzed files; each file declares required usings

## Error Handling

**Patterns:**
- Result types (nullable returns) preferred over exceptions: `JsonContractSchema.Load()` returns `null` on error
- Boolean returns for validation: `bool result = ArrayValidator.ValidateMinItems(...)`
- Sentinel error handling: `ValidationErrorCode.TooManyErrors` used to cap error collection
- No throw statements observed in hot paths; errors collected via `ErrorCollector`
- Defensive null-checking: `if (_errors is not null && _countHolder is not null)`

**Error Communication:**
- `ValidationError` struct holds: `Code`, `Path`, and message string
- `ErrorCollector` exposes errors via indexer: `collector[index]`
- Boolean + side-effect pattern: methods return `bool` and populate error collector

## Logging

**Framework:** No logging observed (zero-allocation library constraint)

**Patterns:**
- No console.WriteLine or ILogger usage
- Errors surfaced via `ErrorCollector` return value
- Comments document intentional behavior instead

## Comments

**When to Comment:**
- Document complex allocations or performance constraints
- Explain invariants: `// Region tracking: ArrayPool-backed arrays instead of Dictionary<int, (int,int)>`
- Clarify encoding behavior: `// RFC 6901: encode ~ as ~0, / as ~1 (order matters — ~ first!)`
- Section headers using visual separators: `// ── ValidateMinItems ──────────────────────────────────────────────────`

**JSDoc/TSDoc:**
- XML documentation comments (`///`) used extensively for public/internal APIs
- Summary tags: `/// <summary>Description here.</summary>`
- Param tags: `/// <param name="ordinal">The schema-determined ordinal for the property.</param>`
- Return tags: `/// <returns>True if valid, false otherwise.</returns>`
- Remarks tags for behavior details: `/// <remarks>Implementations are zero-allocation after warmup.</remarks>`
- Example:
  ```csharp
  /// <summary>
  /// Adds a <see cref="ValidationError"/> to the collector.
  /// When the buffer is full, the last slot is replaced with a TooManyErrors sentinel
  /// and further errors are silently dropped.
  /// </summary>
  /// <param name="error">The validation error to add.</param>
  public void Add(ValidationError error)
  ```

## Function Design

**Size:**
- Methods typically 5-30 lines; larger functions broken into logical sections
- Hot-path methods optimized for inlining (short, simple logic)
- Example: `public bool IsValid => !_errorCollector.HasErrors;` (property, single statement)

**Parameters:**
- Pass by value for small types (int, bool): `ValidateMinItems(3, 2, "/tags", collector)`
- Pass by ref for spans/large buffers (rarely seen in samples)
- Parameters explicitly typed (no var for parameters)
- No default parameter values observed

**Return Values:**
- Boolean for success/failure
- Nullable return for objects: `JsonContractSchema.Load(...) => JsonContractSchema?`
- Struct return for lightweight wrappers: `public ParseResult this[int ordinal]`
- Out parameters used with bool pattern: `TryLoad(json, out var schema)`

## Module Design

**Exports:**
- Single public type per file (convention observed, not enforced): `SchemaNode.cs` exports `SchemaNode`
- Public nested types allowed: `ParseResult.Enumerator` nested in `ParseResult`
- Internal types in separate files for organization

**Barrel Files:**
- Not observed; no index files or aggregating exports
- Each file stands alone

## Struct vs Class Decisions

**Struct Usage (ValueType):**
- `ParseResult`: lightweight wrapper, passed by value, implements `IDisposable`
- `ErrorCollector`: error collection struct, pooled for zero-allocation
- `ValidationError`: simple data holder
- Enumerators: `ParseResult.Enumerator` is struct (enables foreach without allocation)

**Class Usage (RefType):**
- `ArrayBuffer`: shared by reference, stateful, pooled/cached
- `SchemaNode`: immutable tree node, large, allocated once at schema load time

---

*Convention analysis: 2026-03-18*
