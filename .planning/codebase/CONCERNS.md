# Concerns

## Overall Assessment

This is an early-stage project with **extensive documentation but no implementation**. Both source files are empty stubs. The primary concern is execution risk — translating the ambitious design into working code.

## Implementation Gap

**Severity: High**

The codebase has:
- 8 ADRs detailing design decisions
- 6 system invariants
- A glossary with 12 terms
- Detailed README with API examples
- 2 source files that are empty stubs

No runtime code exists. The entire architecture (offset tables, single-pass parsing, schema validation, RFC 6901 paths, dual API surface) remains unbuilt.

## Technical Challenges Ahead

### Zero-allocation parsing
- Building an offset table without heap allocation requires careful use of `Span<T>`, `stackalloc`, and `ArrayPool<T>`
- ADR 8 already rejected `ref struct` due to async incompatibility — navigating this tension between zero-alloc and async support will be key

### Single-pass JSON parser
- Writing a JSON parser that validates against a schema in one pass is non-trivial
- Must handle nested objects, arrays, string escaping, number parsing — all from raw bytes
- Standard `System.Text.Json` readers are forward-only but still allocate in some scenarios

### Error collection during parsing
- Collecting all errors (not fail-fast) while maintaining zero allocation requires pre-allocated error buffers with known maximum sizes

## Missing Infrastructure

- No `.editorconfig` file (referenced in solution items but doesn't exist on disk)
- No CI/CD workflow files (referenced in README badge but `main.yml` not in repo)
- No benchmark project setup (directory exists but empty)
- No `Directory.Build.props` or `Directory.Packages.props` for centralized package management

## No Security Concerns

No runtime code, no external inputs processed, no dependencies with known vulnerabilities. Security will become relevant when the JSON parser handles untrusted input (malicious payloads, deeply nested structures, extremely long strings).

## No Performance Concerns Yet

No code to benchmark. When implemented, key metrics will be:
- Allocation count per parse (must be zero)
- Parse throughput vs System.Text.Json deserialization
- Offset table lookup time
