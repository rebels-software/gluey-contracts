# ADR 3: Single-Pass Validation

## Status
Accepted

## Context
The standard .NET pattern is: deserialize first, validate second. This means traversing the data twice and losing byte-level context by the time validation runs.

## Decision
Schema validation happens during the single parsing pass. As the parser walks the byte buffer, it simultaneously:

1. Validates structure against the schema (types, required fields, constraints)
2. Builds the offset table for accessing values later

If validation fails, parsing still completes the current level to collect all errors — not just the first one. This gives API consumers a complete list of issues in one response.

## Consequences
- Validation rules must be expressible in terms the parser can check during traversal (type, required, min/max length, pattern, enum).
- Complex cross-field validation (e.g., "if field A then field B required") is deferred to the application layer — it cannot be done in a single pass without backtracking.
- Error collection has a configurable maximum to prevent unbounded allocation on malicious input.
