# Glossary

Precise definitions for terms used throughout the Gluey.Contract codebase and documentation.

| Term | Definition |
|------|-----------|
| **Contract** | A schema-based agreement between producer and consumer describing the expected structure, types, and constraints of serialized data. |
| **Schema** | The formal description of a contract — types, constraints, required fields, nesting. For JSON, this is JSON Schema. For other formats, defined by Gluey DSL. |
| **Parsed Data** | The result of a successful parse — an offset table into the original byte buffer, not deserialized objects. |
| **ParsedProperty** | A struct holding an offset and length into the raw byte buffer. Values are materialized only on access (e.g., `GetString()`). |
| **Offset Table** | An internal index mapping property names/paths to byte positions in the original buffer. Built during the single-pass parse. |
| **JSON Pointer** | An [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) string identifying a specific value within a JSON document (e.g., `/devices/0/serialNumber`). Used in validation error paths. |
| **Single-Pass Parse** | Validation and indexing happen in one traversal of the byte buffer — no separate deserialization step followed by validation. |
| **Zero Allocation** | No heap objects are created during parsing. `ParsedProperty` is a value type; the byte buffer is pooled or caller-owned. |
| **Wire Format** | The serialization format of the bytes — JSON, Protobuf, PostgreSQL wire protocol, Redis RESP, etc. Each has a dedicated Gluey.Contract package. |
| **Contract Violation** | A validation failure — the bytes do not conform to the schema. Produces an error with path, code, and message. |
| **Validation Error** | A structured error containing: `Path` (JSON Pointer), `Code` (machine-readable), `Message` (human-readable). |
| **Format Driver** | A Gluey.Contract package that implements byte-reading for a specific wire format (e.g., `Gluey.Contract.Json`). |
| **Buffer** | The raw byte array containing serialized data. Owned by the caller or rented from `ArrayPool<byte>`. Gluey.Contract never copies it. |
