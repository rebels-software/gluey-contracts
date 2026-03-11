# Invariant 5: All Errors Collected Per Parse

## Rule
When parsing fails validation, the parser collects all validation errors in the current level before returning — not just the first error.

## Rationale
API consumers should receive a complete list of issues in one response. Round-tripping to fix errors one at a time is a poor developer experience.

## Verification
- Tests with multiple invalid fields verify all errors are returned.
- Error count in `Result.Failure` matches the number of violations in the input.
- Configurable max error count prevents unbounded collection on malicious input.
