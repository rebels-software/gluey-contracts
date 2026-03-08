# Conventions

## Code Style

- C# 13 with nullable reference types enabled
- Implicit usings enabled
- Global usings in `GlobalUsings.cs` for test projects
- XML doc comments on public types (see `ParsedProperty.cs`, `JsonContractSchema.cs`)

## Type Conventions

- **Core data types:** `readonly struct` (ADR 8) — never `class`, `record struct`, or `ref struct`
- **Format drivers:** `class` (e.g., `JsonContractSchema`)
- **No interfaces on value types** to avoid boxing — use generic constraints: `where T : struct, IParsedProperty`

## Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Namespaces | Match project name | `Gluey.Contract.Json` |
| Types | PascalCase | `ParsedProperty`, `JsonContractSchema` |
| Files | Match type name | `ParsedProperty.cs` |
| Projects | `Gluey.Contract.{Area}` | `Gluey.Contract.Json` |
| Test projects | `{Project}.Tests` | `Gluey.Contract.Tests` |

## Design Patterns

- **Result pattern** over exceptions for expected failures
- **Dual API surface:** `TryParse()` and `Parse()` (ADR 4)
- **Format-agnostic core:** core defines abstractions, format packages implement (ADR 5)
- **Zero-allocation:** no heap allocations in parse path (ADR 2)
- **Single-pass:** validate and index in one traversal (ADR 3)

## Error Handling

- Parse operations never throw (invariant 6)
- All validation errors collected per parse (invariant 5) — not fail-fast
- Errors include RFC 6901 JSON Pointer paths, machine-readable codes, human-readable messages
- Configurable max error count to prevent unbounded allocation on malicious input

## Documentation Standards

- ADRs for all architectural decisions (8 documented)
- Invariants for non-negotiable system rules (6 documented)
- Glossary for precise term definitions (`docs/GLOSSARY.md`)
- README with badges, usage examples, architecture diagram
