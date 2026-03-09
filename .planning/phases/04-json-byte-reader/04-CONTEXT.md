# Phase 4: JSON Byte Reader - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Raw UTF-8 JSON bytes can be tokenized with native byte offset tracking, providing the foundation for zero-allocation validation. Covers READ-01 (byte tokenizer with offset tracking), READ-02 (byte[], Span, Memory inputs), READ-03 (structural validation). Schema-driven validation is Phase 5+. Single-pass walker integration is Phase 9.

</domain>

<decisions>
## Implementation Decisions

### Reader Strategy
- Wrap System.Text.Json's Utf8JsonReader — battle-tested, zero-alloc, already used in Phase 2's schema loader
- JsonByteReader is a ref struct — natural fit since Utf8JsonReader is itself a ref struct, and the reader is consumed on the parse path (not stored in fields)
- Forward-only Read()-in-a-loop design — mirrors Utf8JsonReader pattern, zero allocation, natural for single-pass walker in Phase 9
- Lives in Gluey.Contract.Json package — JSON-specific implementation, consistent with ADR 5 (format-agnostic core)

### Token Output Design
- Minimal per-token info: token type + byte offset + byte length — depth/parent tracking is the walker's job (Phase 9)
- Own JsonByteTokenType enum — decoupled from BCL's JsonTokenType, can tailor values to library needs
- Separate PropertyName token type distinct from String value tokens — walker can trivially distinguish keys from values
- ByteOffset/ByteLength points to content inside quotes for string/property name tokens — matches Phase 1's ParsedProperty contract (offset/length = content inside quotes)

### Error Reporting
- Fail on first structural error — malformed JSON is unrecoverable, subsequent tokens unreliable after structure breaks
- Separate error type (not ValidationErrorCode/ErrorCollector) — structural errors are fundamentally different from schema validation errors (no JSON Pointer path, no schema context)
- Read() returns false when hitting invalid JSON; caller checks reader.Error property for details (byte offset, error kind)
- Error type lives in Gluey.Contract.Json — JSON structural errors are format-specific, future format drivers define their own

### Multi-Input API
- Span-primary: core implementation takes ReadOnlySpan<byte>; byte[] and ReadOnlyMemory<byte> overloads implicitly convert to Span
- Internal visibility — reader is an implementation detail consumed by the walker (Phase 9), not public API
- Reader operates on Span for tokenizing; walker (Phase 9) separately holds byte[] and uses reader's offsets to construct ParsedProperty — clean separation of concerns
- Type named JsonByteReader — emphasizes raw byte reading, distinct from System.Text.Json's Utf8JsonReader

### Claude's Discretion
- JsonByteTokenType enum values and whether to split Number into Integer/Number
- JsonReadError readonly struct field layout and error kind enum values
- How the Utf8JsonReader wrapper extracts byte offsets (TokenStartIndex, BytesConsumed, etc.)
- Internal helper methods for offset calculation around quoted strings
- Whether to expose CurrentDepth from the underlying Utf8JsonReader for convenience

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `JsonSchemaLoader.cs`: Already wraps Utf8JsonReader for schema loading — same wrapping pattern applies
- `ParsedProperty.cs`: Stores byte[] + offset + length pointing to content inside quotes — reader's offset contract must match
- `ValidationErrorCode.cs`: Full enum defined — structural errors deliberately kept separate
- `ErrorCollector.cs`: ArrayPool-backed error collection — used by schema validation, not structural errors

### Established Patterns
- Utf8JsonReader with ValueTextEquals and u8 literals for zero-alloc matching (Phase 2 loader)
- readonly struct for public types, internal sealed class for tree nodes (ADR 8)
- Dual API: TryLoad/Load, TryParse/Parse — reader uses Read() loop pattern instead
- No external dependencies in core (ADR 7) — System.Text.Json is BCL, acceptable in JSON package

### Integration Points
- Phase 9 (Single-Pass Walker) is the primary consumer — calls Read() in a loop, uses offsets to build OffsetTable and ParsedProperty values
- Phase 5+ (Validation) uses token type to check JSON type matches schema type keyword
- ParsedProperty constructor takes byte[] + offset + length — walker provides byte[], reader provides offset/length
- JsonContractSchema.TryParse/Parse currently stubs — Phase 9 will wire the reader into these methods

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-json-byte-reader*
*Context gathered: 2026-03-09*
