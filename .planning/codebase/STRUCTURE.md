# Directory Structure

## Layout

```
Gluey.Contracts/
+-- Gluey.Contract.sln
+-- README.md
+-- LICENSE                          (Apache 2.0)
+-- NOTICE
+-- assets/
|   +-- icon.png                     (NuGet package icon)
+-- docs/
|   +-- GLOSSARY.md                  (Term definitions)
|   +-- adr/                         (8 Architecture Decision Records)
|   |   +-- 1-context-scope-and-goals.md
|   |   +-- 2-zero-allocation-design.md
|   |   +-- 3-single-pass-validation.md
|   |   +-- 4-dual-api-surface.md
|   |   +-- 5-format-agnostic-core.md
|   |   +-- 6-json-pointer-paths.md
|   |   +-- 7-no-external-dependencies.md
|   |   +-- 8-readonly-structs-over-classes.md
|   +-- invariants/                  (6 system invariants)
|       +-- README.md
|       +-- 1-zero-allocations-in-parse-path.md
|       +-- 2-buffer-ownership-by-caller.md
|       +-- 3-paths-precomputed-from-schema.md
|       +-- 4-no-external-dependencies-in-core.md
|       +-- 5-all-errors-collected-per-parse.md
|       +-- 6-parse-never-throws.md
+-- src/
|   +-- Gluey.Contract/             (Core library)
|   |   +-- Gluey.Contract.csproj
|   |   +-- ParsedProperty.cs       (empty readonly struct stub)
|   +-- Gluey.Contract.Json/        (JSON format driver)
|       +-- Gluey.Contract.Json.csproj
|       +-- JsonContractSchema.cs   (empty class stub)
+-- tests/
|   +-- Gluey.Contract.Tests/
|   |   +-- Gluey.Contract.Tests.csproj
|   |   +-- GlobalUsings.cs
|   +-- Gluey.Contract.Json.Tests/
|       +-- Gluey.Contract.Json.Tests.csproj
|       +-- GlobalUsings.cs
+-- benchmarks/                      (empty, reserved)
```

## Key Locations

| What | Where |
|------|-------|
| Solution file | `Gluey.Contract.sln` |
| Core library | `src/Gluey.Contract/` |
| JSON driver | `src/Gluey.Contract.Json/` |
| Core tests | `tests/Gluey.Contract.Tests/` |
| JSON tests | `tests/Gluey.Contract.Json.Tests/` |
| ADRs | `docs/adr/` |
| Invariants | `docs/invariants/` |
| Glossary | `docs/GLOSSARY.md` |

## Naming Conventions

- **Projects:** `Gluey.Contract.{SubArea}` (e.g., `Gluey.Contract.Json`)
- **Tests:** `{ProjectName}.Tests` mirroring source project
- **Namespaces:** Match project names exactly (e.g., `Gluey.Contract.Json`)
- **ADRs:** Numbered sequentially, kebab-case: `{N}-{title}.md`
- **Invariants:** Numbered sequentially, kebab-case: `{N}-{title}.md`

## File Count

- 2 source files (both stubs)
- 0 test files (only GlobalUsings)
- 8 ADR documents
- 7 invariant documents (including README)
- 1 glossary
- Heavy documentation-to-code ratio — all design exists as docs, no implementation yet
