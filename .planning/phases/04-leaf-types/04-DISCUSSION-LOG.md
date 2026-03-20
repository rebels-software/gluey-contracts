# Phase 4: Leaf Types - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-20
**Phase:** 04-leaf-types
**Areas discussed:** String encoding handling, Enum accessor design, Bit field extraction, Padding behavior

---

## String Encoding Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Use node.Encoding field | GetString() dispatches based on stored encoding byte (0=UTF-8, 1=ASCII) | |
| Always UTF-8 | Treat all binary strings as UTF-8 | |
| Honor encoding field (Recommended) | Use Encoding.ASCII for "ASCII", Encoding.UTF8 for "UTF-8" | ✓ |

**User's choice:** Honor encoding field
**Notes:** User initially questioned whether encoding matters since C# strings are UTF-16 internally. Clarified that the distinction matters at decode time (bytes > 127 behave differently).

| Option | Description | Selected |
|--------|-------------|----------|
| Trim trailing nulls | "Hello\0\0\0" returns "Hello" | |
| Return raw including nulls | Consumer must trim | |
| Add mode to ADR-16 now | Extend contract spec with string mode | ✓ |

**User's choice:** Add mode field to ADR-16. Modes: plain, trimStart, trimEnd, trim.

| Option | Description | Selected |
|--------|-------------|----------|
| trimEnd (Recommended) | Default mode when not specified | ✓ |
| plain | No trimming by default | |

**User's choice:** trimEnd as default

---

## Enum Accessor Design

| Option | Description | Selected |
|--------|-------------|----------|
| Two ParsedProperty entries (Recommended) | "mode" + "modes" in OffsetTable | ✓ |
| One entry with dual accessor | Single entry, GetString/GetUInt8 both work | |

**User's choice:** Two entries
**Notes:** User corrected the convention: parsed["mode"] = numeric (raw byte), parsed["modes"] = string label. This is INVERTED from ADR-16 text. ADR-16 must be updated.

| Option | Description | Selected |
|--------|-------------|----------|
| Lookup table on ParsedProperty | Store values dict reference, lazy GetString() | ✓ |
| Pre-resolved string at parse time | Allocate label during Parse() | |

**User's choice:** Initially chose pre-resolved, then switched to deferred (lazy) when allocation concern was raised.

| Option | Description | Selected |
|--------|-------------|----------|
| Return numeric as string (Recommended) | Unmapped value 42 returns "42" | ✓ |
| Return null | Explicit about missing mapping | |
| Throw InvalidOperationException | Strict contract violation | |

**User's choice:** Return numeric as string

---

## Bit Field Extraction

| Option | Description | Selected |
|--------|-------------|----------|
| Flat namespace | parsed["isCharging"] — top-level access | |
| Nested under container | parsed["status/isCharging"] — path-based | ✓ |

**User's choice:** Nested under container name

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, returns raw container byte(s) | parsed["status"].GetUInt8() | ✓ |
| No, only sub-fields | Container not accessible | |

**User's choice:** Container accessible with raw bytes

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-extracted at parse time (Recommended) | Extract sub-field values during Parse() | ✓ |
| Deferred extraction in GetXxx() | Bit manipulation at access time | |

**User's choice:** Pre-extracted at parse time

---

## Padding Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Skip entirely (Recommended) | No OffsetTable entry, invisible | |
| Empty ParsedProperty entry | Entry exists but returns empty | ✓ |

**User's choice:** Empty ParsedProperty entry — padding fields exist in NameToOrdinal but return empty

| Option | Description | Selected |
|--------|-------------|----------|
| Named (current ADR-16 behavior) | Named like "reserved1", "gap" | ✓ |
| Anonymous padding | No names needed | |

**User's choice:** Named (current behavior)

---

## Claude's Discretion

- How to store enum values dictionary reference on ParsedProperty
- How to store string mode on ParsedProperty or BinaryContractNode
- Internal method organization for bit extraction helpers
- Test contract JSON structure

## Deferred Ideas

None
