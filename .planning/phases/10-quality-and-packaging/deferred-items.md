# Deferred Items - Phase 10

## Flaky Array Tests (ArrayPool Buffer Reuse)

**Discovered during:** 10-02 Task 2
**Tests:** `Array_Foreach_YieldsAllElements`, `ArrayElement_ArrayOfObjects_NestedAccess`
**Issue:** Tests intermittently fail when run as part of the full test suite. Pass consistently in isolation.
**Root cause:** ArrayPool buffer reuse across test fixtures -- when one test disposes a ParseResult (returning buffers with `clearArray: true`), another test may rent the same zeroed buffer, affecting array element data.
**Impact:** CI may show intermittent failures in array tests.
**Fix:** Array tests should create fresh buffers or use test isolation to prevent ArrayPool cross-contamination.
