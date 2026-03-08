# Feature Research

**Domain:** Zero-allocation, schema-driven JSON byte validation and indexing library (.NET)
**Researched:** 2026-03-08
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume any JSON Schema validator has. Missing these means the library is not considered usable.

#### JSON Schema Draft 2020-12 Core Keywords

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `type` keyword (null, boolean, integer, number, string, array, object) | Fundamental — every schema uses it | LOW | Single byte-range check per value |
| `enum` / `const` | Basic value matching | MEDIUM | Requires byte-level comparison without deserializing |
| `required` | Most object schemas use this | LOW | Track seen properties during parse |
| `properties` / `patternProperties` / `additionalProperties` | Core object validation | HIGH | `patternProperties` needs regex matching on raw bytes |
| `items` / `prefixItems` | Core array validation (Draft 2020-12 renamed `additionalItems` to `items`, added `prefixItems`) | MEDIUM | Must track array index during single-pass |
| `allOf` / `anyOf` / `oneOf` / `not` | Composition — users combine schemas constantly | HIGH | `oneOf` requires evaluating all branches to ensure exactly one matches |
| `$ref` / `$defs` | Schema reuse — nearly every non-trivial schema uses `$ref` | HIGH | Must resolve references at schema load time, not parse time |
| `if` / `then` / `else` | Conditional validation — widely adopted since Draft 7 | HIGH | Requires tracking which branch matched to apply correct sub-schema |

#### Validation Keywords

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `minimum` / `maximum` / `exclusiveMinimum` / `exclusiveMaximum` | Numeric constraints | MEDIUM | Must parse numbers from bytes without allocation |
| `multipleOf` | Numeric divisibility | LOW | Straightforward after number parsing |
| `minLength` / `maxLength` | String length constraints | MEDIUM | Must count Unicode codepoints, not bytes, in UTF-8 |
| `pattern` | Regex matching on strings | MEDIUM | Regex runs against string value; may need temporary materialization |
| `minItems` / `maxItems` | Array size constraints | LOW | Counter during array walk |
| `uniqueItems` | Array uniqueness | HIGH | Comparing elements without deserialization is expensive; needs hashing strategy |
| `minProperties` / `maxProperties` | Object size constraints | LOW | Counter during object walk |
| `contains` / `minContains` / `maxContains` | Array element matching | MEDIUM | Must track match count during array walk |
| `dependentRequired` / `dependentSchemas` | Property dependencies | MEDIUM | Must track which properties were seen |

#### Error Reporting

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Collect all errors (not fail-fast) | Users need to see every problem, not just the first | MEDIUM | Pre-allocated error buffer with max cap (64 per PROJECT.md) |
| RFC 6901 JSON Pointer instance paths | Standard error location format; every major validator does this | MEDIUM | Paths like `/address/street`; must be precomputed from schema to avoid allocation |
| Error code + human-readable message | Programmatic handling + developer readability | LOW | Enum-based codes, static messages |
| Schema location in errors | Users need to know which schema keyword failed | MEDIUM | Evaluation path pointing to the failing keyword |

#### API Surface

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Accept `byte[]` input | Most common raw-byte representation | LOW | Core input type |
| Accept `ReadOnlySpan<byte>` input | High-performance .NET APIs use spans | LOW | Zero-copy view |
| Accept `ReadOnlyMemory<byte>` input | Async-compatible byte buffer | LOW | Needed because `readonly struct` cannot hold `Span<T>` |
| Schema loading from JSON string/bytes | Must be able to construct schemas | MEDIUM | Separate from validation path; allocation acceptable here |
| Boolean validation result (pass/fail) | Simplest possible API for "is this valid?" | LOW | Flag output format per JSON Schema spec |

### Differentiators (Competitive Advantage)

Features that set Gluey.Contract apart from JsonSchema.Net, Corvus.JsonSchema, and NJsonSchema.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Zero heap allocations on parse path | No other .NET JSON Schema validator achieves true zero-alloc validation. Corvus gets close with code-gen but still allocates for dynamic schemas. JsonSchema.Net and NJsonSchema allocate freely. | HIGH | Core invariant. Requires `stackalloc`, `ArrayPool<T>`, pre-computed paths. Enforced by benchmark tests. |
| Single-pass validation + indexing | Competitors validate OR parse, never both simultaneously. Gluey validates and builds an offset table in one pass. | HIGH | The defining feature. Walk bytes once, get both validation result and indexed access. |
| Offset-based value access (ParsedProperty) | Values read on-demand from original buffer via offset/length. No deserialization until the caller asks. | MEDIUM | `readonly struct` with `GetString()`, `GetInt32()`, etc. that slice into caller-owned buffer. |
| RFC 6901 JSON Pointer paths precomputed from schema | Competitors build paths during validation (allocating strings). Gluey precomputes all possible paths from the schema at schema-load time. | MEDIUM | Invariant 3. Paths are known statically from schema structure. |
| Parse-never-throws contract | Validation failures returned as values, never exceptions. Competitors throw on invalid input or use mixed models. | LOW | Invariant 6. `Result<T>` pattern or `TryParse` boolean. |
| Format-agnostic core | Core types work with any wire format (JSON, Protobuf, Postgres wire). Competitors are JSON-only. | MEDIUM | ADR 5. `Gluey.Contract` has zero format assumptions; `Gluey.Contract.Json` is just one driver. |
| Buffer ownership by caller | Library never copies the byte buffer. Competitors typically copy input into internal structures. | LOW | Invariant 2. Enables zero-copy pipeline integration. |
| Dual API surface (TryParse + Result) | Idiomatic .NET (`TryParse` returning `bool`) alongside functional (`Result<T>`). Most validators offer only one style. | LOW | ADR 4. Two entry points, same validation engine underneath. |

### Anti-Features (Deliberately NOT Building)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Full deserialization to POCO/objects | "I want typed objects" | Defeats zero-allocation purpose. Allocates one object per property. System.Text.Json already does this well. | Provide `ParsedProperty.GetXxx()` for on-demand value materialization from bytes. Users deserialize only what they need. |
| Schema generation from C# types | "Generate schema from my class" | Huge surface area, already solved by NJsonSchema and JsonSchema.Net. Pulls in reflection dependencies. | Accept standard JSON Schema documents. Let users generate schemas with existing tools. |
| Code generation (a la Corvus) | "Generate C# types from schema" | Requires Roslyn dependency, MSBuild integration, source generator infrastructure. Completely different product. | Corvus.JsonSchema already does this excellently. Gluey focuses on runtime dynamic validation. |
| Stream/async input processing | "I want to validate streaming JSON" | Breaks single-pass-over-contiguous-buffer model. Span/Memory cannot span stream chunks. Massively increases complexity. | Require caller to buffer input first. Document this constraint. Revisit in future milestone if demand exists. |
| Full JSON Schema output format (Hierarchical/List) | "I want spec-compliant output format" | The spec output format is deeply nested JSON, requiring allocation. Over-engineered for the 90% case. | Provide flat error list with instance path, schema path, code, and message. Sufficient for programmatic and human consumption. |
| `$vocabulary` meta-schema validation | "Validate that a meta-schema is correct" | Meta-schema validation is a schema-authoring tool concern, not a runtime validation concern. | Accept schemas as-is. Validate data against schemas, not schemas against meta-schemas. |
| Remote `$ref` resolution (HTTP fetching) | "My schema references external URLs" | Introduces HTTP dependency, caching complexity, security concerns (SSRF). Violates no-external-dependencies constraint. | Provide a schema registry where users pre-register schemas by URI. All `$ref` resolution is local. |
| Business/domain validation rules | "Validate that email is not already taken" | Application-layer concern. Mixing domain logic into schema validation creates coupling. | Schema validates structure. Application validates business rules. Compose both in the application layer. |
| Format assertion by default | "format should reject invalid emails" | Draft 2020-12 treats `format` as annotation by default. Making it assert changes spec compliance behavior. | Support format-annotation vocabulary by default. Allow opt-in to format-assertion via configuration. |

## JSON Schema Draft 2020-12 Complete Keyword Reference

All keywords that a compliant implementation must handle, organized by vocabulary.

### Core Vocabulary (Required)

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `$schema` | Identifies dialect | P1 — needed to detect draft version |
| `$id` | Schema URI identity | P1 — needed for `$ref` resolution |
| `$ref` | Static schema reference | P1 — nearly every schema uses this |
| `$defs` | Schema definitions | P1 — companion to `$ref` |
| `$anchor` | Named reference target | P2 — less common than `$ref`/`$defs` |
| `$dynamicRef` | Dynamic reference | P3 — advanced feature, rare in practice |
| `$dynamicAnchor` | Dynamic reference target | P3 — companion to `$dynamicRef` |
| `$vocabulary` | Meta-schema vocabulary declaration | P3 — only relevant for meta-schema authors |
| `$comment` | Human-readable comment | P1 — trivially ignored during validation |

### Applicator Vocabulary

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `allOf` | All sub-schemas must match | P1 |
| `anyOf` | At least one must match | P1 |
| `oneOf` | Exactly one must match | P1 |
| `not` | Must not match | P1 |
| `if` / `then` / `else` | Conditional application | P2 |
| `properties` | Named property schemas | P1 |
| `patternProperties` | Regex-matched property schemas | P2 |
| `additionalProperties` | Schema for unmatched properties | P1 |
| `propertyNames` | Schema for property name strings | P2 |
| `dependentSchemas` | Conditional schemas based on property presence | P2 |
| `prefixItems` | Positional array item schemas | P1 |
| `items` | Schema for remaining array items | P1 |
| `contains` | At least one array item must match | P2 |

### Unevaluated Vocabulary

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `unevaluatedProperties` | Schema for properties not evaluated by other keywords | P3 — requires annotation tracking across applicators |
| `unevaluatedItems` | Schema for array items not evaluated by other keywords | P3 — requires annotation tracking across applicators |

### Validation Vocabulary

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `type` | Primitive type assertion | P1 |
| `enum` | Value must be one of listed values | P1 |
| `const` | Value must equal specified value | P1 |
| `multipleOf` | Numeric divisibility | P1 |
| `maximum` / `exclusiveMaximum` | Numeric upper bounds | P1 |
| `minimum` / `exclusiveMinimum` | Numeric lower bounds | P1 |
| `maxLength` / `minLength` | String length bounds | P1 |
| `pattern` | String regex match | P1 |
| `maxItems` / `minItems` | Array size bounds | P1 |
| `uniqueItems` | Array element uniqueness | P2 |
| `maxContains` / `minContains` | Contains match count bounds | P2 |
| `maxProperties` / `minProperties` | Object property count bounds | P1 |
| `required` | Required property names | P1 |
| `dependentRequired` | Conditional required properties | P2 |

### Format Vocabulary (Annotation by Default)

| Keyword Value | Validates | Implementation Priority |
|---------------|-----------|------------------------|
| `date-time` | RFC 3339 date-time | P2 |
| `date` | RFC 3339 full-date | P2 |
| `time` | RFC 3339 time | P2 |
| `duration` | RFC 3339 duration | P3 |
| `email` | Email address | P2 |
| `hostname` | RFC 1034 hostname | P3 |
| `ipv4` | IPv4 dotted-quad | P2 |
| `ipv6` | IPv6 address | P2 |
| `uri` | RFC 3986 URI | P2 |
| `uri-reference` | URI or relative reference | P3 |
| `iri` / `iri-reference` | Internationalized URI | P3 |
| `uri-template` | RFC 6570 URI template | P3 |
| `uuid` | RFC 4122 UUID | P2 |
| `json-pointer` | RFC 6901 JSON Pointer | P2 |
| `regex` | ECMA 262 regex | P3 |

### Meta-Data Vocabulary (Annotations Only)

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `title` | Human-readable name | P3 — annotation, no validation effect |
| `description` | Human-readable description | P3 — annotation |
| `default` | Default value | P3 — annotation |
| `deprecated` | Deprecation flag | P3 — annotation |
| `readOnly` / `writeOnly` | Access intent | P3 — annotation |
| `examples` | Example values | P3 — annotation |

### Content Vocabulary (Annotations Only)

| Keyword | Purpose | Implementation Priority |
|---------|---------|------------------------|
| `contentMediaType` | MIME type of string content | P3 — annotation |
| `contentEncoding` | Encoding (e.g., base64) | P3 — annotation |
| `contentSchema` | Schema for decoded content | P3 — annotation |

## Feature Dependencies

```
Schema Loading (parse JSON Schema document)
    |
    +--requires--> $ref Resolution (resolve references in schema)
    |                  |
    |                  +--requires--> Schema Registry (store schemas by URI)
    |
    +--requires--> Path Precomputation (build RFC 6901 paths from schema tree)
    |
    +--enables--> Single-Pass Validation + Indexing
                      |
                      +--requires--> JSON Byte Walker (tokenize raw bytes)
                      |
                      +--requires--> Type Validators (type, enum, const)
                      |
                      +--requires--> Numeric Validators (min, max, multipleOf)
                      |
                      +--requires--> String Validators (minLength, maxLength, pattern)
                      |
                      +--requires--> Array Validators (items, prefixItems, contains, minItems, maxItems)
                      |
                      +--requires--> Object Validators (properties, required, additionalProperties)
                      |
                      +--requires--> Composition Validators (allOf, anyOf, oneOf, not)
                      |
                      +--requires--> Error Collection (pre-allocated buffer, max 64)
                      |
                      +--produces--> Offset Table (property name -> byte position mapping)
                      |                  |
                      |                  +--enables--> ParsedProperty (offset + length into buffer)
                      |                                    |
                      |                                    +--enables--> On-Demand Value Materialization
                      |
                      +--produces--> Validation Result (Result<T> / bool)

Conditional Validators (if/then/else, dependentSchemas)
    +--requires--> Composition Validators (reuses branch evaluation)

Unevaluated Keywords (unevaluatedProperties, unevaluatedItems)
    +--requires--> Annotation Tracking (which properties/items were evaluated)
    +--requires--> All Applicator Keywords (must know what was "evaluated")

Format Assertion (opt-in)
    +--requires--> Format-specific Validators (date-time parser, email parser, etc.)
    +--conflicts--> Zero-allocation goal (regex/parsing may allocate)
```

### Dependency Notes

- **Schema Loading requires $ref Resolution:** Schemas cannot be used until all `$ref` pointers are resolved to their targets. This MUST happen at schema-load time, not at validation time, to avoid allocation during parse.
- **Single-Pass Validation requires JSON Byte Walker:** The byte-level tokenizer is the foundation. Without it, nothing else works.
- **Unevaluated keywords require annotation tracking:** `unevaluatedProperties` and `unevaluatedItems` need to know which properties/items were already validated by `properties`, `patternProperties`, `additionalProperties`, `items`, `prefixItems`, and `contains`. This is the hardest part of the spec to implement.
- **Format assertion conflicts with zero-allocation:** Some format validators (email, URI, regex) may need to allocate. This tension means format assertion should be opt-in, with annotation as the default.
- **Path Precomputation enables zero-alloc error reporting:** If paths are computed at schema-load time, the validation path never needs to build path strings.

## MVP Definition

### Launch With (v1)

Minimum viable product -- what's needed for the library to be genuinely useful.

- [ ] JSON byte walker (tokenize raw UTF-8 bytes, no allocation) -- foundation for everything
- [ ] Schema loading from JSON bytes/string -- must be able to consume schemas
- [ ] `$ref` / `$defs` resolution at schema-load time -- nearly every schema uses references
- [ ] Path precomputation from schema -- enables zero-alloc error reporting (invariant 3)
- [ ] `type` / `enum` / `const` validation -- most fundamental constraints
- [ ] `properties` / `required` / `additionalProperties` -- core object validation
- [ ] `items` / `prefixItems` -- core array validation
- [ ] `minimum` / `maximum` / `exclusiveMinimum` / `exclusiveMaximum` / `multipleOf` -- numeric constraints
- [ ] `minLength` / `maxLength` / `pattern` -- string constraints
- [ ] `minItems` / `maxItems` -- array size constraints
- [ ] `minProperties` / `maxProperties` -- object size constraints
- [ ] `allOf` / `anyOf` / `oneOf` / `not` -- schema composition
- [ ] Error collection with RFC 6901 paths, error codes, messages -- table stakes for usability
- [ ] Offset table construction during validation -- the differentiating feature
- [ ] `ParsedProperty` with `GetString()`, `GetInt32()`, `GetBoolean()`, etc. -- on-demand materialization
- [ ] `Result<T>` and `TryParse` API surface -- dual API (ADR 4)
- [ ] Accept `byte[]`, `ReadOnlySpan<byte>`, `ReadOnlyMemory<byte>` -- input flexibility
- [ ] BenchmarkDotNet suite proving zero allocation -- must be demonstrable

### Add After Validation (v1.x)

Features to add once core is working and validated.

- [ ] `if` / `then` / `else` conditional validation -- widely used but not in every schema
- [ ] `dependentRequired` / `dependentSchemas` -- property dependencies
- [ ] `patternProperties` / `propertyNames` -- advanced object validation
- [ ] `contains` / `minContains` / `maxContains` -- array element matching
- [ ] `uniqueItems` -- array uniqueness (allocation-sensitive, needs hashing strategy)
- [ ] `$anchor` resolution -- less common than `$ref`/`$defs`
- [ ] Schema registry for multi-schema resolution -- pre-register schemas by URI
- [ ] Format annotation support (record format value, no assertion) -- spec default behavior
- [ ] Common format assertions as opt-in (`date-time`, `email`, `uuid`, `uri`, `ipv4`, `ipv6`) -- user demand driven
- [ ] Configurable max error count -- currently hardcoded at 64
- [ ] Nested object/array offset table access (`data["address"]["street"]`) -- deep path traversal

### Future Consideration (v2+)

Features to defer until the library has adoption and feedback.

- [ ] `unevaluatedProperties` / `unevaluatedItems` -- requires full annotation tracking; hardest part of spec
- [ ] `$dynamicRef` / `$dynamicAnchor` -- rare in practice; complex runtime resolution
- [ ] Content vocabulary (`contentMediaType`, `contentEncoding`, `contentSchema`) -- niche use case
- [ ] Meta-data annotations (`title`, `description`, `default`, `deprecated`) -- annotation collection API
- [ ] Remaining format validators (`duration`, `hostname`, `iri`, `uri-template`, `regex`) -- long tail
- [ ] Custom keyword extension API -- allow users to add custom validation keywords
- [ ] JSON Schema output format (List/Hierarchical) -- spec-compliant output structure
- [ ] Gluey.Contract.AspNetCore integration -- model binding, filter pipeline
- [ ] Gluey.Contract.Protobuf driver -- second wire format
- [ ] Performance-critical regex caching for `pattern` / `patternProperties` -- optimization pass

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| JSON byte walker | HIGH | HIGH | P1 |
| Schema loading + `$ref` resolution | HIGH | HIGH | P1 |
| Path precomputation | HIGH | MEDIUM | P1 |
| `type` / `enum` / `const` | HIGH | LOW | P1 |
| `properties` / `required` / `additionalProperties` | HIGH | MEDIUM | P1 |
| `items` / `prefixItems` | HIGH | MEDIUM | P1 |
| Numeric constraints | HIGH | MEDIUM | P1 |
| String constraints | HIGH | MEDIUM | P1 |
| Array/object size constraints | MEDIUM | LOW | P1 |
| `allOf` / `anyOf` / `oneOf` / `not` | HIGH | HIGH | P1 |
| Error collection + RFC 6901 paths | HIGH | MEDIUM | P1 |
| Offset table + ParsedProperty | HIGH | HIGH | P1 |
| Dual API (TryParse + Result) | MEDIUM | LOW | P1 |
| BenchmarkDotNet zero-alloc proof | HIGH | LOW | P1 |
| `if` / `then` / `else` | MEDIUM | MEDIUM | P2 |
| `dependentRequired` / `dependentSchemas` | MEDIUM | MEDIUM | P2 |
| `patternProperties` / `propertyNames` | MEDIUM | MEDIUM | P2 |
| `contains` / `minContains` / `maxContains` | MEDIUM | MEDIUM | P2 |
| `uniqueItems` | LOW | HIGH | P2 |
| Schema registry | MEDIUM | MEDIUM | P2 |
| Format annotation/assertion | LOW | HIGH | P2 |
| `unevaluatedProperties` / `unevaluatedItems` | LOW | HIGH | P3 |
| `$dynamicRef` / `$dynamicAnchor` | LOW | HIGH | P3 |
| Custom keyword extensions | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for launch -- without these the library is not usable
- P2: Should have, add in v1.x -- important for spec completeness and adoption
- P3: Nice to have, future consideration -- advanced features or rare use cases

## Competitor Feature Analysis

| Feature | JsonSchema.Net | Corvus.JsonSchema | NJsonSchema | Gluey.Contract |
|---------|---------------|-------------------|-------------|----------------|
| Draft 2020-12 support | Full | Full | Partial (draft 4+) | Full (planned) |
| Zero allocation | No | Near-zero (code-gen only) | No | Yes (design invariant) |
| Single-pass validate+index | No (validate only) | No (validate only) | No (validate only) | Yes (core differentiator) |
| Offset-based access | No | No | No | Yes (ParsedProperty) |
| Dynamic schema validation | Yes | Yes (separate assembly) | Yes | Yes |
| Code generation | No | Yes (primary mode) | Yes | No (anti-feature) |
| Schema generation from types | No | No | Yes | No (anti-feature) |
| Error detail level | Flag/List/Hierarchy | Boolean + detailed | List | Flat list with paths |
| Input type | JsonElement/JsonNode | JsonElement | JToken/string | Raw bytes |
| External dependencies | System.Text.Json | System.Text.Json | Newtonsoft.Json | None in core |
| Format assertion | Opt-in | Yes | Partial | Opt-in (planned) |
| `unevaluatedProperties` | Yes | Yes | No | Deferred (v2+) |
| Custom vocabularies | Yes | Via code-gen | No | Deferred (v2+) |
| NuGet downloads | ~15M | ~500K | ~200M | New |

### Key Competitive Insight

No existing .NET library operates directly on raw bytes. All competitors require the input to first be parsed into `JsonElement`, `JsonNode`, or `JToken` -- which itself allocates. Gluey.Contract's position is unique: it is the only library that takes raw `byte[]` and produces validation results + indexed access without any intermediate object model. This is the fundamental differentiator.

## Sources

- [JSON Schema Draft 2020-12 Core](https://json-schema.org/draft/2020-12/json-schema-core)
- [JSON Schema Draft 2020-12 Validation](https://json-schema.org/draft/2020-12/json-schema-validation)
- [JSON Schema Draft 2020-12 Release Notes](https://json-schema.org/draft/2020-12/release-notes)
- [JsonSchema.Net Documentation](https://docs.json-everything.net/schema/basics/)
- [Corvus.JsonSchema GitHub](https://github.com/corvus-dotnet/Corvus.JsonSchema)
- [NJsonSchema GitHub](https://github.com/RicoSuter/NJsonSchema)
- [.NET 10 JSON Schema Performance](https://endjin.com/blog/2025/10/how-dotnet-10-boosted-json-schema-performance-by-18-percent)
- [JSON Schema Format Vocabulary](https://www.learnjsonschema.com/2020-12/format-annotation/format/)
- [System.Text.Json Utf8JsonReader](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader)
- [Interpreting JSON Schema Output](https://json-schema.org/blog/posts/interpreting-output)

---
*Feature research for: Zero-allocation schema-driven JSON byte validation and indexing*
*Researched: 2026-03-08*
