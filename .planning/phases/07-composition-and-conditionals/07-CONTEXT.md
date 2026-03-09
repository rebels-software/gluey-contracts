# Phase 7: Composition and Conditionals - Context

**Gathered:** 2026-03-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Schema composition keywords (allOf, anyOf, oneOf, not), conditional keywords (if/then/else), and dependency keywords (dependentRequired, dependentSchemas) enable complex validation logic. Covers VALD-09, VALD-10, VALD-11. These validators receive pre-computed subschema results from the walker (Phase 9) rather than raw values. Advanced keywords are Phase 8. The single-pass walker that orchestrates these validators is Phase 9.

</domain>

<decisions>
## Implementation Decisions

### Subschema Evaluation Model
- Walker drives all subschema evaluation — composition validators do NOT call subschema evaluation themselves
- Walker evaluates subschemas, passes pre-computed results (bool pass/fail per subschema) to validator methods
- Validators apply composition logic only: "all passed?" / "at least one?" / "exactly one?" / "none passed?"
- Silent (bool-only) evaluation mode: walker evaluates subschemas without collecting errors into the main ErrorCollector — used for not, anyOf, oneOf, and if-schema evaluation
- This is a new evaluation mode for the walker (Phase 9) — leaf validators in Phase 5/6 don't need it

### Error Reporting
- All inner subschema errors are suppressed for all composition, conditional, and dependency keywords
- Only top-level errors reported: AllOfInvalid, AnyOfInvalid, OneOfInvalid, NotInvalid, IfThenInvalid, IfElseInvalid, DependentRequiredMissing, DependentSchemaInvalid
- Generic static messages only (already defined in ValidationErrorMessages, except IfElseInvalid and DependentSchemaInvalid which are new)
- OneOfInvalid used for both "zero matched" and "multiple matched" — single error code
- dependentRequired errors use root object path (SchemaNode.Path), consistent with RequiredMissing

### if/then/else Semantics
- 'if' subschema evaluated in same bool-only silent mode as composition keywords — no error collection
- If 'if' passes and 'then' exists → evaluate 'then' (errors suppressed, report IfThenInvalid on failure)
- If 'if' fails and 'else' exists → evaluate 'else' (errors suppressed, report IfElseInvalid on failure)
- If 'then'/'else' missing for the relevant branch → no-op (walker skips, per JSON Schema spec)
- If 'if' present without both 'then' and 'else' → no validation effect (annotation only)
- New error code: IfElseInvalid with message "Value failed the if condition and did not match the else schema."

### Validator Class Organization
- Three new internal static classes in Gluey.Contract.Json:
  - `CompositionValidator` — ValidateAllOf, ValidateAnyOf, ValidateOneOf, ValidateNot (receive bool[] or pass count from walker)
  - `ConditionalValidator` — ValidateIfThen, ValidateIfElse (receive bool result of then/else evaluation from walker)
  - `DependencyValidator` — ValidateDependentRequired, ValidateDependentSchemas (receive set of present property names)
- Each follows the established pattern: `internal static bool ValidateX(..., ErrorCollector collector)`
- DependencyValidator receives property name set (HashSet<string> or similar) — walker already tracks property names for 'required' validation

### New Error Codes Required
- `IfElseInvalid` — "Value failed the if condition and did not match the else schema."
- `DependentSchemaInvalid` — "Value does not match the dependent schema."

### Claude's Discretion
- Exact method signatures for composition validators (how bool results are passed — bool[], int passCount, etc.)
- Internal helpers for property name collection in dependency validation
- Test organization and test helper utilities
- Whether CompositionValidator methods take individual bools or arrays

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SchemaNode.cs`: All composition/conditional fields already present — AllOf (SchemaNode[]), AnyOf (SchemaNode[]), OneOf (SchemaNode[]), Not (SchemaNode?), IfSchema, ThenSchema, ElseSchema, DependentRequired (Dictionary<string, string[]>), DependentSchemas (Dictionary<string, SchemaNode>)
- `JsonSchemaLoader.cs`: Already parses all Phase 7 keywords including dependentRequired
- `ValidationErrorCode.cs`: AllOfInvalid, AnyOfInvalid, OneOfInvalid, NotInvalid, IfThenInvalid, DependentRequiredMissing already defined — need to add IfElseInvalid and DependentSchemaInvalid
- `ValidationErrorMessages.cs`: Messages for existing codes already defined — need entries for new codes
- `SchemaRefResolver.cs`: Already walks AllOf/AnyOf/OneOf/DependentSchemas for ref resolution
- `SchemaIndexer.cs`: Already indexes through AllOf/AnyOf/OneOf/DependentSchemas for ordinal assignment
- `KeywordValidator.ValidateRequired()`: Establishes the pattern for property presence checking

### Established Patterns
- `internal static class` with `internal static bool ValidateX()` methods returning pass/fail
- Direct error push to ErrorCollector — validators never check IsFull
- SchemaNode.Path for precomputed RFC 6901 JSON Pointer paths
- Validators are stateless pure functions — no instance state, no side effects beyond error collection

### Integration Points
- Phase 9 (Single-Pass Walker): primary consumer — needs to implement bool-only silent evaluation mode for subschema checking
- Walker tracks property names during object traversal (for 'required') — same data feeds DependencyValidator
- ErrorCollector passed through from walker to validators — same instance for entire parse

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

*Phase: 07-composition-and-conditionals*
*Context gathered: 2026-03-09*
