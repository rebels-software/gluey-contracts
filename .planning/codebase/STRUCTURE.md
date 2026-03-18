# Codebase Structure

**Analysis Date:** 2026-03-18

## Directory Layout

```
gluey-contracts/
├── src/
│   ├── Gluey.Contract/                      # Core: schema model, parsing primitives
│   │   ├── Schema/                          # Schema tree & compilation
│   │   ├── Parsing/                         # ParsedProperty, OffsetTable, ParseResult
│   │   ├── Buffers/                         # ArrayBuffer (array element storage)
│   │   └── Validation/                      # ErrorCollector, ValidationError, error codes
│   │
│   └── Gluey.Contract.Json/                 # JSON-specific parser & validators
│       ├── Reader/                          # JsonByteReader, token types, read errors
│       ├── Schema/                          # JsonContractSchema, SchemaWalker, loaders
│       └── Validators/                      # Per-keyword validators (9 files)
│
├── tests/
│   ├── Gluey.Contract.Tests/                # Core unit tests
│   └── Gluey.Contract.Json.Tests/           # JSON parser & validator tests
│
├── benchmarks/
│   └── Gluey.Contract.Benchmarks/           # BenchmarkDotNet performance measurements
│
├── docs/                                    # Documentation
├── assets/                                  # Icon, branding
├── .github/                                 # GitHub workflows (CI/CD)
└── Gluey.Contract.sln                       # Solution file

```

## Directory Purposes

**`src/Gluey.Contract/`:**
- Purpose: Core library interfaces and data structures, agnostic to wire format
- Contains: Schema nodes, parsing/indexing primitives, error collection, buffer management
- Key files: `SchemaNode.cs`, `ParsedProperty.cs`, `ParseResult.cs`, `OffsetTable.cs`, `ErrorCollector.cs`

**`src/Gluey.Contract/Schema/`:**
- Purpose: Schema representation and metadata
- Contains: `SchemaNode` (immutable tree node with all JSON Schema Draft 2020-12 keyword properties), `SchemaType` (enum of JSON types), `SchemaOptions` (config), `SchemaRegistry` (cross-schema reference registry)
- Key files: `SchemaNode.cs`, `SchemaType.cs`, `SchemaRegistry.cs`

**`src/Gluey.Contract/Parsing/`:**
- Purpose: Property access and parse result assembly
- Contains: `ParsedProperty` (readonly struct: offset + length + navigation), `OffsetTable` (ArrayPool-backed ordinal→property mapping), `ParseResult` (public composite: table + errors)
- Key files: `ParsedProperty.cs`, `OffsetTable.cs`, `ParseResult.cs`

**`src/Gluey.Contract/Buffers/`:**
- Purpose: Array element storage with region tracking
- Contains: `ArrayBuffer` (thread-static cached ArrayPool-backed storage for array elements, grouped by ordinal)
- Key files: `ArrayBuffer.cs`

**`src/Gluey.Contract/Validation/`:**
- Purpose: Error representation and collection
- Contains: `ValidationError` (code + path + message), `ValidationErrorCode` (enum), `ValidationErrorMessages` (message mapping), `ErrorCollector` (ArrayPool-backed error buffer)
- Key files: `ValidationError.cs`, `ValidationErrorCode.cs`, `ValidationErrorMessages.cs`, `ErrorCollector.cs`

**`src/Gluey.Contract.Json/`:**
- Purpose: JSON-specific parsing and validation (pluggable format layer)
- Contains: JSON tokenizer, schema loader, single-pass walker, keyword validators
- Key files: `JsonContractSchema.cs` (public API), `SchemaWalker.cs` (orchestrator)

**`src/Gluey.Contract.Json/Reader/`:**
- Purpose: Convert raw UTF-8 bytes to typed tokens with byte offsets
- Contains: `JsonByteReader` (wraps Utf8JsonReader, tracks offsets), `JsonByteTokenType` (token enum), `JsonReadError`/`JsonReadErrorKind` (malformed JSON reporting)
- Key files: `JsonByteReader.cs`, `JsonByteTokenType.cs`, `JsonReadError.cs`

**`src/Gluey.Contract.Json/Schema/`:**
- Purpose: JSON Schema loading, compilation, and single-pass walking
- Contains:
  - `JsonContractSchema` — Public API for load/parse
  - `JsonSchemaLoader` — Parses JSON Schema documents into SchemaNode tree
  - `SchemaIndexer` — Assigns ordinals to properties, compiles Regex patterns
  - `SchemaRefResolver` — Resolves `$ref` pointers
  - `SchemaWalker` — Orchestrates single-pass validation and offset table construction
- Key files: `JsonContractSchema.cs`, `JsonSchemaLoader.cs`, `SchemaIndexer.cs`, `SchemaRefResolver.cs`, `SchemaWalker.cs`

**`src/Gluey.Contract.Json/Validators/`:**
- Purpose: Pluggable validation logic for each JSON Schema keyword family
- Contains: 9 validator files, each with static methods for specific keyword(s)
  - `KeywordValidator.cs` — type, enum, const validation
  - `NumericValidator.cs` — min/max, multipleOf, exclusive bounds
  - `StringValidator.cs` — minLength, maxLength, pattern
  - `ArrayValidator.cs` — minItems, maxItems, uniqueItems
  - `ObjectValidator.cs` — required, minProperties, maxProperties, properties/patternProperties
  - `CompositionValidator.cs` — allOf, anyOf, oneOf, not
  - `ConditionalValidator.cs` — if/then/else
  - `DependencyValidator.cs` — dependentRequired, dependentSchemas
  - `FormatValidator.cs` — format keyword assertion (when enabled)
- Key files: All 9 `.cs` files

**`tests/Gluey.Contract.Tests/`:**
- Purpose: Unit tests for core data structures and functionality
- Contains: Tests for OffsetTable, ErrorCollector, ParseResult, and core contract types
- Naming convention: Test files named after tested class (e.g., `OffsetTableTests.cs`)

**`tests/Gluey.Contract.Json.Tests/`:**
- Purpose: Integration and unit tests for JSON parsing, loading, and validation
- Contains: 40+ test classes covering:
  - `JsonByteReaderTests.cs` — Tokenizer behavior
  - `JsonSchemaLoadingTests.cs` — Schema compilation
  - `JsonContractSchemaApiTests.cs` — Public Parse/Load API
  - Keyword validator tests: `KeywordValidatorTypeTests.cs`, `KeywordValidatorEnumConstTests.cs`, `KeywordValidatorObjectTests.cs`, `KeywordValidatorArrayTests.cs`, etc.
  - Feature tests: `NestedPropertyAccessTests.cs`, `ArrayElementAccessTests.cs`, etc.
  - Allocation tests: `TryParseAllocationTests.cs`, `PropertyAccessAllocationTests.cs`, `DisposeAllocationTests.cs`
- Naming convention: Test files named after feature/validator being tested

**`benchmarks/Gluey.Contract.Benchmarks/`:**
- Purpose: BenchmarkDotNet performance measurements (Gluey vs. STJ + JsonSchema.Net)
- Contains: Benchmark classes measuring parse time and allocation across small/medium/large payloads

## Key File Locations

**Entry Points:**
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — `Load()`, `TryLoad()`, `Parse()`, `TryParse()` public methods
- Schema loading begins here; single-pass parsing initiated here

**Configuration:**
- `src/Gluey.Contract/Schema/SchemaOptions.cs` — Schema loading options (error capacity, format assertion)
- `src/Gluey.Contract.Json/Schema/JsonSchemaLoader.cs` — Parsing options for JSON Schema dialect

**Core Logic:**
- `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` — Heart of single-pass validation; orchestrates all walking and validation
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — Zero-allocation property accessor interface
- `src/Gluey.Contract/Buffers/ArrayBuffer.cs` — Array element storage and thread-static caching

**Testing:**
- `tests/Gluey.Contract.Json.Tests/JsonContractSchemaApiTests.cs` — Public API contract tests
- `tests/Gluey.Contract.Json.Tests/AllocationTests/` — Allocation verification tests
- Test fixtures defined inline (strings/bytes) or via `GlobalUsings.cs` shared imports

## Naming Conventions

**Files:**
- Pascal case: `JsonContractSchema.cs`, `SchemaWalker.cs`, `ArrayValidator.cs`
- Test files: `[TestedClass]Tests.cs` (e.g., `OffsetTableTests.cs`, `ArrayValidatorTests.cs`)
- Internal helper files: `[Topic][Detail].cs` (e.g., `JsonReadError.cs`, `ValidationErrorCode.cs`)

**Directories:**
- Feature-grouped: `Schema/`, `Parsing/`, `Validators/`, `Reader/`, `Buffers/`, `Validation/`
- Package-scoped: Parallel structure in core (`Gluey.Contract`) and JSON plugin (`Gluey.Contract.Json`)
- Test-mirrored: `tests/` mirror `src/` structure with parallel namespaces

**Namespaces:**
- Root: `Gluey.Contract` (core public API)
- JSON format: `Gluey.Contract.Json` (public JSON API)
- Tests: `Gluey.Contract.Tests`, `Gluey.Contract.Json.Tests`

**Classes:**
- Pascal case: `SchemaNode`, `ParsedProperty`, `ErrorCollector`, `JsonContractSchema`
- Structs marked as data holders: `ParsedProperty`, `ParseResult`, `OffsetTable`, `ErrorCollector`
- Ref structs for hot-path: `SchemaWalker`, `JsonByteReader`

**Methods:**
- Pascal case public: `Load()`, `Parse()`, `GetString()`, `Add()`
- try- pattern for error handling: `TryLoad()`, `TryParse()`, `TryGetValue()`
- Internal static factory: `Rent()`, `Walk()`

## Where to Add New Code

**New JSON Schema Keyword Validation:**
1. Check if keyword family already covered in `src/Gluey.Contract.Json/Validators/`
2. If new family: Create `[KeywordFamily]Validator.cs` in `Validators/` directory
3. Implement static `Validate[Keyword]()` methods following existing pattern (return bool, push errors)
4. Add dispatch call in `SchemaWalker` private walk methods (object walk, array walk, etc.)
5. Add unit tests in `tests/Gluey.Contract.Json.Tests/[KeywordFamily]ValidatorTests.cs`

**New Format Parser (e.g., Protobuf, RESP):**
1. Create new package: `src/Gluey.Contract.[Format]/`
2. Mirror structure: `Reader/` (tokenizer), `Schema/` (format-specific schema loader and walker), `Validators/` (format-specific keyword validation if needed)
3. Expose public API class (analogous to `JsonContractSchema`) that loads schema and exposes `Parse()` method
4. Return `ParseResult` with same structure for uniform property access
5. Create parallel test project: `tests/Gluey.Contract.[Format].Tests/`

**New Shared Data Structure:**
1. If applicable to multiple formats: Add to `src/Gluey.Contract/` (core)
2. If JSON-specific: Add to `src/Gluey.Contract.Json/`
3. Use ArrayPool for rented buffers; implement IDisposable if managing resources
4. Use readonly struct for stack-allocation if possible (except Buffers which are thread-cached classes)

**New Public API Method:**
1. On `JsonContractSchema.cs` for JSON, or equivalent format class
2. Follow existing try-pattern or direct-return pattern for consistency
3. Ensure never throws exceptions (exception-free API design)
4. Add corresponding method to `IJsonContractSchema` (if interface exists) or follow convention

**New Test:**
1. Test file location mirrors source location (e.g., `tests/Gluey.Contract.Json.Tests/[Feature]Tests.cs`)
2. Use `[TestFixture]` on class, `[Test]` on methods (NUnit3 convention)
3. Use FluentAssertions for readability
4. Tests for hot-path features (validators, walkers) should verify zero allocation (see `AllocationTests/`)

**Global Settings/Constants:**
- Format-specific: `SchemaOptions.cs` in core, or equivalent in format package
- Error messages: `ValidationErrorMessages.cs` with static lookup

## Special Directories

**`src/Gluey.Contract/obj/`, `src/Gluey.Contract/bin/`:**
- Purpose: Build output (compiled assemblies, debug symbols)
- Generated: Yes
- Committed: No (git-ignored)

**`tests/*/obj/`, `tests/*/bin/`:**
- Purpose: Test project build output
- Generated: Yes
- Committed: No (git-ignored)

**`docs/`:**
- Purpose: Documentation (API docs, architecture guides, design rationale)
- Generated: No (manually maintained)
- Committed: Yes

**`.github/workflows/`:**
- Purpose: GitHub Actions CI/CD pipeline definitions
- Generated: No (manually maintained)
- Committed: Yes
- Key files: Main workflow for build, test, NuGet publish

**`benchmarks/Gluey.Contract.Benchmarks/obj/`, `benchmarks/Gluey.Contract.Benchmarks/bin/`:**
- Purpose: Benchmark build output
- Generated: Yes
- Committed: No (git-ignored)

---

*Structure analysis: 2026-03-18*
