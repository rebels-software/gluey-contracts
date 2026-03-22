# ADR 16: Binary Format Contract

## Status
Proposed

## Context
Gluey.Contract currently supports JSON via `Gluey.Contract.Json`. Many IoT and embedded systems communicate using custom binary protocols where every byte is accounted for — minimizing traffic costs is critical. There is no standard schema language for arbitrary binary layouts the way JSON Schema describes JSON.

Teams defining binary protocols today resort to prose documentation, spreadsheets, or C header files. None of these are machine-readable, versionable, or validatable at runtime.

## Decision
Introduce a new format package (`Gluey.Contract.Binary`) backed by a JSON-based contract definition that describes arbitrary binary message layouts. The contract is the single source of truth for how a byte buffer is parsed, validated, and exposed via `ParsedProperty`.

### Contract structure

A binary contract is a JSON document with metadata and a `fields` map. Each field describes one segment of the byte payload.

```json
{
  "kind": "binary",
  "id": "dunamis/battery/stateUpdate",
  "name": "stateUpdate",
  "version": "0.0.1",
  "displayName": {
    "en-US": "Periodically sent message which carries information about battery state",
    "pl": "Cyklicznie wysyłana wiadomość, która niesie informację o stanie baterii"
  },
  "endianness": "little",
  "fields": {
    "recordedAgo": {
      "type": "uint16",
      "size": 2,
      "displayName": {
        "en-US": "Value representing how many seconds ago data was recorded",
        "pl": "Liczba całkowita reprezentująca ile sekund temu dane zostały przeczytane"
      },
      "validation": { "min": 0, "max": 3600 }
    },
    "flags": {
      "dependsOn": "recordedAgo",
      "type": "bits",
      "size": 2,
      "fields": {
        "isCharging": { "bit": 0, "bits": 1, "type": "boolean" },
        "errorCode": { "bit": 1, "bits": 4, "type": "uint8" }
      }
    },
    "level": {
      "dependsOn": "flags",
      "type": "uint8",
      "size": 1,
      "displayName": {
        "en-US": "Integer value indicating percent of battery state",
        "pl": "Liczba całkowita reprezentująca procent naładowania baterii"
      },
      "validation": { "min": 0, "max": 100 }
    },
    "sensorReading": {
      "dependsOn": "level",
      "type": "int32",
      "size": 3,
      "endianness": "big"
    },
    "operatorBadgeId": {
      "dependsOn": "sensorReading",
      "type": "string",
      "encoding": "ASCII",
      "mode": "trimEnd",
      "size": 6,
      "displayName": {
        "en-US": "Unique identifier of forklift operator's badge",
        "pl": "Identyfikator operatora, który jest najbliżej wózka widłowego"
      },
      "validation": { "pattern": "^[A-Z0-9]+$" }
    },
    "mode": {
      "dependsOn": "operatorBadgeId",
      "type": "enum",
      "primitive": "uint8",
      "size": 1,
      "values": {
        "0": "idle",
        "1": "charging",
        "2": "discharging"
      }
    },
    "lastThreeVoltages": {
      "dependsOn": "mode",
      "type": "array",
      "count": 3,
      "element": {
        "type": "float32",
        "size": 4
      }
    },
    "errorCount": {
      "dependsOn": "lastThreeVoltages",
      "type": "uint8",
      "size": 1
    },
    "recentErrors": {
      "dependsOn": "errorCount",
      "type": "array",
      "count": "errorCount",
      "element": {
        "type": "struct",
        "size": 5,
        "fields": {
          "code": {
            "type": "uint16",
            "size": 2
          },
          "severity": {
            "dependsOn": "code",
            "type": "uint8",
            "size": 1
          },
          "timestamp": {
            "dependsOn": "severity",
            "type": "uint16",
            "size": 2,
            "endianness": "big"
          }
        }
      }
    },
    "firmwareHash": {
      "dependsOn": "recentErrors",
      "type": "string",
      "encoding": "ASCII",
      "size": 8
    }
  }
}
```

### Dependency chain model

Fields form a singly-linked chain via `dependsOn`. The parser computes byte offsets by walking this chain — no field declares an absolute offset.

- **Root field**: Exactly one field has no `dependsOn`. It starts at byte offset 0.
- **Chained fields**: Each field starts immediately after its parent ends.
- **Single child rule**: A field can have at most one dependent. No field is shared by multiple children.
- **Scoped chains**: Struct fields inside array elements form their own independent chain. The root of a struct sub-chain has no `dependsOn` within that scope.

This model means JSON key order does not matter — the parser reconstructs the read order from the dependency graph.

### Supported types

| Type | Size | Notes |
|------|------|-------|
| `uint8` | 1 | Unsigned 8-bit integer |
| `uint16` | 2 | Unsigned 16-bit integer |
| `uint32` | 4 | Unsigned 32-bit integer |
| `int8` | 1 | Signed 8-bit integer |
| `int16` | 2 | Signed 16-bit integer |
| `int32` | 4 | Signed 32-bit integer |
| `float32` | 4 | IEEE 754 single-precision |
| `float64` | 8 | IEEE 754 double-precision |
| `boolean` | 1 | 0 = false, non-zero = true |
| `string` | explicit `size` | Requires `encoding` (ASCII, UTF-8); optional `mode` for trimming |
| `enum` | from `primitive` | Maps integer keys to string values |
| `bits` | explicit `size` | Container for bit-level sub-fields |
| `array` | computed | Fixed or semi-dynamic element collection |
| `struct` | explicit `size` | Container for nested field chains |
| `padding` | explicit `size` | Explicit gap between fields, not exposed in ParsedObject |

**Truncated numerics**: A field may declare a `size` smaller than its type's natural width. For example, `int32` with `size: 3` reads 3 bytes and sign-extends to 32 bits. The `size` is always explicit and required.

### Endianness

- Contract-level `endianness` sets the default for all numeric fields.
- Individual fields may override with their own `endianness` property.
- Applies to multi-byte numeric types only.

### String modes

String fields support a `mode` property that controls whitespace and null-byte trimming:

| Mode | Behavior |
|------|----------|
| `plain` | Return bytes as-is, no trimming |
| `trimStart` | Remove leading null bytes (0x00) |
| `trimEnd` | Remove trailing null bytes (0x00) and whitespace |
| `trim` | Remove both leading null bytes and trailing null bytes/whitespace |

When `mode` is omitted, `trimEnd` is the default. This handles the common case of fixed-length fields padded with null bytes.

### Arrays

Arrays support two count modes:

- **Fixed**: `count` is a number. Element count is known at contract definition time. Total array byte size = `count * element.size`.
- **Semi-dynamic**: `count` is a string referencing another field's name. The referenced field must be a numeric type and must appear earlier in the dependency chain. Element count is resolved at parse time.

Fully dynamic arrays (unknown count, no terminator) are not supported.

### Enums

An enum field stores an integer in the payload and maps it to a string via the `values` table. Both representations are exposed in ParsedObject:

- `parsed["mode"]` returns the raw numeric value (`1`) via the standard numeric accessor (e.g., `GetUInt8()`)
- `parsed["modes"]` returns the mapped string label (`"charging"`) via `GetString()` — the label accessor is the field name suffixed with `s`

### Bit fields

A `bits` container spans one or more bytes and contains sub-fields at specific bit positions. Each sub-field declares:

- `bit`: starting bit position (0-indexed from LSB)
- `bits`: width in bits
- `type`: the interpretation type (`boolean`, `uint8`, etc.)

Multi-byte bit containers (e.g., 16 bits across 2 bytes) are supported.

### Validation

Fields support validation rules similar to JSON Schema:

| Rule | Applies to | Description |
|------|-----------|-------------|
| `min` / `max` | numeric types | Inclusive value range |
| `pattern` | string | Regex pattern match |
| `minLength` / `maxLength` | string | Byte length constraints |

Contract-level validation (enforced when loading the contract, not at parse time):

- Exactly one root field (no `dependsOn`)
- No cycles in the dependency graph
- Each field has at most one child
- Semi-dynamic array `count` references an existing numeric field earlier in the chain
- Enum `values` keys are valid integers within the `primitive` range
- Bit sub-fields do not overlap and fit within the container `size`
- `size` is explicitly declared on every field

### Parsed property access

Parsed fields are accessed via path syntax matching the dependency/nesting structure:

```
parsed["recordedAgo"]                  -> 120
parsed["flags/isCharging"]             -> true
parsed["flags/errorCode"]              -> 3
parsed["level"]                        -> 87
parsed["sensorReading"]                -> -12345
parsed["operatorBadgeId"]              -> "ABC123"
parsed["mode"]                         -> 1 (raw numeric value)
parsed["modes"]                        -> "charging" (mapped string label)
parsed["lastThreeVoltages/0"]          -> 12.5
parsed["lastThreeVoltages/1"]          -> 12.3
parsed["lastThreeVoltages/2"]          -> 12.1
parsed["errorCount"]                   -> 2
parsed["recentErrors/0/code"]          -> 404
parsed["recentErrors/0/severity"]      -> 2
parsed["recentErrors/0/timestamp"]     -> 58000
parsed["recentErrors/1/code"]          -> 500
parsed["firmwareHash"]                 -> "A1B2C3D4"
```

Path segments use `/` as separator. Array elements are indexed by position. This mirrors JSON Pointer semantics, making binary-to-JSON conversion straightforward.

### Parse only — no serialization by default

The binary parser is read-only: `byte[] -> ParsedObject`. Serialization (`object -> byte[]`) is a separate concern that requires the contract to describe how to write — this may be added in a future package but is out of scope for the initial implementation.

The parsed result integrates with existing Gluey extension points. For example, the JSON extension's `ToJson()` method works naturally since ParsedObject exposes named fields via path-based access.

## Consequences
- A new package `Gluey.Contract.Binary` is added, depending on `Gluey.Contract` core.
- Binary contracts are defined in JSON files, loadable at runtime — no code generation or compile-time step required.
- The dependency chain model eliminates absolute offsets, making contracts resilient to field reordering in the JSON definition.
- Semi-dynamic arrays enable variable-length messages while keeping the contract fully declarative.
- The same `ParsedProperty` / `ParsedObject` interface is used for both JSON and binary formats — consuming code does not need to know the wire format.
- Contract validation catches structural errors (cycles, missing references, overlapping bits) at load time, before any parsing occurs.
