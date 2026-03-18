# Architecture

**Analysis Date:** 2026-03-18

## Pattern Overview

**Overall:** Single-Pass Schema-Driven Validation with Zero-Allocation Offset Indexing

**Key Characteristics:**
- Single-pass validation and indexing of raw UTF-8 bytes (no deserialization)
- Zero heap allocation during parse path using ArrayPool-backed buffers
- RFC 6901 JSON Pointer paths for all validation errors and property access
- Immutable, pre-compiled schema trees loaded at startup
- Modular validator plug-in architecture (one validator per JSON Schema keyword family)
- Two-phase architecture: schema loading/compilation (startup), then single-pass walking (hot path)

## Layers

**Schema Layer:**
- Purpose: Compile and validate JSON Schema documents at startup, build hierarchical schema tree
- Location: `src/Gluey.Contract/Schema/`, `src/Gluey.Contract.Json/Schema/`
- Contains: `SchemaNode` (immutable tree), `JsonSchemaLoader` (parses JSON Schema), `SchemaIndexer` (ordinal assignment), `SchemaRefResolver` (resolves `$ref`), `SchemaRegistry` (cross-schema references)
- Depends on: Nothing (pure schema compilation)
- Used by: JSON walking/validation engine, all consumers loading schemas

**Byte Reading Layer:**
- Purpose: Tokenize raw UTF-8 bytes into logical JSON tokens while tracking byte offsets
- Location: `src/Gluey.Contract.Json/Reader/`
- Contains: `JsonByteReader` (wraps `Utf8JsonReader`, tracks byte offsets), `JsonByteTokenType` (enum), `JsonReadError`/`JsonReadErrorKind`
- Depends on: System.Text.Json
- Used by: SchemaWalker during single-pass validation

**Validation Layer:**
- Purpose: Validate JSON tokens against schema keywords in isolation; compose into full validation
- Location: `src/Gluey.Contract.Json/Validators/`
- Contains: Specialized validators for each keyword family (type, enum, const, numeric ranges, strings, arrays, objects, composition, conditionals, dependencies, formats)
- Depends on: `SchemaNode`, `JsonByteReader`
- Used by: SchemaWalker to validate individual tokens/properties

**Walking/Indexing Layer:**
- Purpose: Orchestrate single-pass walk, dispatch to validators, build offset table for property access
- Location: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Contains: `SchemaWalker` (ref struct, coordinates walk), `WalkResult` (outcome of walk)
- Depends on: Byte reading layer, validation layer, offset/error collection infrastructure
- Used by: JsonContractSchema.Parse()

**Core Data Structures Layer:**
- Purpose: Zero-allocation property indexing, error collection, offset/buffer management
- Location: `src/Gluey.Contract/Parsing/`, `src/Gluey.Contract/Buffers/`, `src/Gluey.Contract/Validation/`
- Contains:
  - `ParsedProperty` (readonly struct: offset + length into byte buffer, child/array navigation)
  - `OffsetTable` (ArrayPool-backed ordinal→ParsedProperty mapping)
  - `ArrayBuffer` (thread-static cached storage for array elements, region-tracked by ordinal)
  - `ErrorCollector` (ArrayPool-backed error buffer with overflow sentinel)
  - `ParseResult` (composite return: offset table + errors + name→ordinal mapping)
- Depends on: System.Buffers
- Used by: All layers for result assembly

**Public API Layer:**
- Purpose: High-level, exception-free schema loading and parsing for library consumers
- Location: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs`
- Contains: `JsonContractSchema` (load schema, parse bytes, all try-/Load patterns)
- Depends on: All lower layers
- Used by: External code consuming the library

## Data Flow

**Schema Load & Compilation (One-Time at Startup):**

1. User calls `JsonContractSchema.Load(schemaJson)` with JSON Schema as bytes or string
2. `JsonSchemaLoader` parses the JSON and builds `SchemaNode` tree (recursive descent)
3. `SchemaIndexer` walks the tree, assigns ordinal indices to all named properties, pre-compiles Regex patterns
4. `SchemaRefResolver` resolves all `$ref` pointers to their target nodes
5. Result: Immutable `JsonContractSchema` holds the compiled tree + name→ordinal mapping

**Single-Pass Validation & Indexing (Hot Path):**

1. User calls `schema.Parse(jsonBytes)`
2. `SchemaWalker` (ref struct) created, initializes:
   - `JsonByteReader` wrapping input bytes
   - `OffsetTable` (ArrayPool-rented) for property storage
   - `ArrayBuffer` (thread-static cached) for array elements
   - `ErrorCollector` (ArrayPool-rented) for errors
3. Walker reads tokens from `JsonByteReader`, walks left-to-right over tokens
4. For each token, walker dispatches to appropriate validator (type, enum, numeric, etc.)
5. Validators return bool (success/failure) and push errors if validation fails
6. For properties matching the schema, walker records offset + length in `OffsetTable`
7. For arrays, walker adds elements to `ArrayBuffer` grouped by ordinal
8. At end of walk, walker returns `WalkResult` (table, errors, array buffer, structural error flag)
9. `JsonContractSchema.Parse()` wraps result in `ParseResult` and returns to user
10. User disposes `ParseResult` → cascades to `OffsetTable.Dispose()` + `ErrorCollector.Dispose()` + `ArrayBuffer.Dispose()` → all buffers returned to ArrayPool

**Property Access at Rest (Zero-Allocation, On-Demand Materialization):**

1. User gets `ParseResult`, calls `result["propertyName"]` or `result[ordinal]`
2. String key → looked up in name→ordinal mapping → ordinal returned
3. Ordinal used to index `OffsetTable`, retrieves `ParsedProperty` struct (offset + length)
4. User calls `ParsedProperty.GetString()`, `GetInt32()`, etc. → materializes value from byte buffer on demand
5. For nested/array access: `ParsedProperty` carries child ordinal mapping or `ArrayBuffer` reference
   - `ParsedProperty["childName"]` → looks up in child ordinals → returns new `ParsedProperty` pointing to same byte buffer at different offset
   - `ParsedProperty[index]` → consults `ArrayBuffer` → gets array element `ParsedProperty`
6. All navigation uses stack-allocated structs (no heap allocation)

**State Management:**

- **Schema state:** Immutable `SchemaNode` tree, built once, reused across many parses
- **Parse state:** Stack-allocated `SchemaWalker` ref struct (no heap escaping), allocated buffers rented from ArrayPool, returned when walker exits scope
- **Result state:** User holds `ParseResult` struct containing references to ArrayPool buffers; buffers returned when `ParseResult.Dispose()` called
- **Thread safety:** `ArrayBuffer` uses thread-static cache (`t_cached`) — one instance per thread, reused within same thread across parses

## Key Abstractions

**SchemaNode:**
- Purpose: Immutable tree node representing compiled JSON Schema (Draft 2020-12) keyword fields at a particular path
- Examples: `src/Gluey.Contract/Schema/SchemaNode.cs`
- Pattern: Property-based immutable record; pre-computed RFC 6901 path; pre-compiled Regex patterns for validation keywords; nested children as properties; PropertyLookup table for UTF8 byte matching

**ParsedProperty:**
- Purpose: Zero-allocation accessor into raw byte buffer; struct holding offset + length + path + child/array navigation metadata
- Examples: `src/Gluey.Contract/Parsing/ParsedProperty.cs`
- Pattern: Readonly struct (stack-allocated); contains two constructors (leaf vs. navigable); indexers for child property and array element access; Get* methods (GetString, GetInt32, etc.) for on-demand materialization; includes nested ArrayEnumerator for foreach over arrays

**OffsetTable & ArrayBuffer:**
- Purpose: ArrayPool-backed property storage, ordinal-indexed for schema properties; region-tracked storage for array elements
- Examples: `src/Gluey.Contract/Parsing/OffsetTable.cs`, `src/Gluey.Contract/Buffers/ArrayBuffer.cs`
- Pattern: Struct (OffsetTable) and class (ArrayBuffer) wrapping ArrayPool rentals; Set/Get/Count operations; IDisposable to return rented buffers; thread-static cache on ArrayBuffer for reuse

**ErrorCollector:**
- Purpose: ArrayPool-backed buffer for collecting ValidationError during parse without heap allocation; sentinel overflow handling
- Examples: `src/Gluey.Contract/Validation/ErrorCollector.cs`
- Pattern: Struct wrapping ArrayPool rental; Add() pushes errors; when capacity exceeded, last slot replaced with TooManyErrors sentinel; GetEnumerator for struct-based foreach; IDisposable

**Validators (per-keyword):**
- Purpose: Encapsulate validation logic for JSON Schema keyword families in isolation
- Examples: `src/Gluey.Contract.Json/Validators/KeywordValidator.cs`, `NumericValidator.cs`, `StringValidator.cs`, `ArrayValidator.cs`, `ObjectValidator.cs`, `CompositionValidator.cs`, `ConditionalValidator.cs`, `DependencyValidator.cs`, `FormatValidator.cs`
- Pattern: Static methods returning bool; consume SchemaNode + JsonByteTokenType/bytes; push errors to ErrorCollector if validation fails; no state, no allocation

**SchemaWalker:**
- Purpose: Orchestrate single-pass validation and offset table construction; dispatch tokens to validators; manage parse state
- Examples: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Pattern: Internal ref struct (stack-allocated, cannot escape); two static entry points (byte[] vs. ReadOnlySpan); Execute() method for walk orchestration; recursive private methods for object/array descent

## Entry Points

**Schema Loading:**
- Location: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` - `Load()` / `TryLoad()` methods
- Triggers: Called once at application startup or when schema source changes
- Responsibilities: Parse JSON Schema, build and validate tree, return ready-to-parse schema object

**Parsing/Validation:**
- Location: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` - `Parse()` / `TryParse()` methods
- Triggers: Called per incoming JSON byte array (hot path)
- Responsibilities: Create SchemaWalker, perform single-pass walk, return ParseResult with offset table + errors

**Property Access:**
- Location: `src/Gluey.Contract/Parsing/ParseResult.cs` - indexers `[string name]` / `[int ordinal]`
- Triggers: User code accessing parsed properties
- Responsibilities: Resolve name to ordinal via schema mapping, return ParsedProperty; or directly index by ordinal

**Error Inspection:**
- Location: `src/Gluey.Contract/Parsing/ParseResult.cs` - `Errors` property, `IsValid` property
- Triggers: When parse result has validation errors
- Responsibilities: Expose ErrorCollector with enumerable error list

## Error Handling

**Strategy:** Exception-free API. All methods return nullable/struct results. Errors collected in ErrorCollector, inspected after parse.

**Patterns:**

- **Structural errors (malformed JSON):** `SchemaWalker` sets `HasStructuralError` flag in `WalkResult`, returns `null` from `JsonContractSchema.Parse()` to indicate the JSON cannot be parsed
- **Validation errors (schema violations):** Collected in `ErrorCollector` during walk; accessible via `ParseResult.Errors`; includes RFC 6901 path and error code for each failure
- **Capacity limits (too many errors):** When `ErrorCollector` reaches capacity (default 64), last slot replaced with sentinel `ValidationErrorCode.TooManyErrors` and further errors dropped silently
- **Out-of-bounds/missing properties:** `ParsedProperty.Empty` returned (default struct) for missing properties; never throw; `HasValue` property allows checking before materialization

## Cross-Cutting Concerns

**Logging:** None. Library is zero-allocation critical; no logging infrastructure.

**Validation:** Multi-layered:
- Schema validation during load phase (JSON Schema syntax, reference resolution, pattern compilation)
- Token validation during parse phase (per-keyword validators)
- Combined errors include path context (RFC 6901)

**Authentication:** Not applicable. Library validates structure and types, not identity/authorization.

**Memory Management:**
- ArrayPool for OffsetTable, ErrorCollector, ArrayBuffer entries — rented at walk start, returned when parse result disposed
- Thread-static cache on ArrayBuffer for per-thread reuse across multiple parses
- Stack allocation (ref structs) for SchemaWalker, JsonByteReader, enumerators — zero heap pressure
- Regex patterns compiled once at schema load time, reused across all parses

---

*Architecture analysis: 2026-03-18*
