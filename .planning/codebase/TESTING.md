# Testing Patterns

**Analysis Date:** 2026-03-18

## Test Framework

**Runner:**
- NUnit 4.3.1
- Config: `.csproj` files declare `<IsTestProject>true</IsTestProject>`

**Assertion Library:**
- FluentAssertions 8.0.1

**Run Commands:**
```bash
dotnet test                    # Run all tests
dotnet test --watch           # Watch mode
dotnet test --collect:"XPlat Code Coverage"  # Coverage
```

**Supporting Packages:**
- `Microsoft.NET.Test.Sdk` 17.12.0 — test host
- `NUnit3TestAdapter` 4.6.0 — NUnit adapter for VS/CLI
- `coverlet.collector` 6.0.2 — code coverage collection
- `NUnit.Analyzers` 4.5.0 — analyzer for test best practices

## Test File Organization

**Location:**
- Co-located with source in parallel directory: `src/` and `tests/`
- Two test projects: `Gluey.Contract.Tests` and `Gluey.Contract.Json.Tests`
- Path: `tests/Gluey.Contract.Json.Tests/` mirrors `src/Gluey.Contract/`

**Naming:**
- Test files: `[UnitUnderTest]Tests.cs`
- Examples: `ArrayValidatorTests.cs`, `JsonContractSchemaApiTests.cs`, `DisposeAllocationTests.cs`
- Test classes: same as filename (PascalCase with `Tests` suffix)

**Structure:**
```
tests/Gluey.Contract.Json.Tests/
├── AllocationTests/
│   ├── DisposeAllocationTests.cs
│   ├── FormatAssertionAllocationTests.cs
│   ├── PropertyAccessAllocationTests.cs
│   └── TryParseAllocationTests.cs
├── ArrayElementAccessTests.cs
├── ArrayValidatorTests.cs
├── CompositionValidatorTests.cs
├── ConditionalValidatorTests.cs
├── ContainsValidatorTests.cs
├── DependencyValidatorTests.cs
├── FormatValidatorTests.cs
├── JsonByteReaderTests.cs
├── JsonContractSchemaApiTests.cs
├── JsonSchemaLoadingTests.cs
├── KeywordValidatorArrayTests.cs
├── KeywordValidatorEnumConstTests.cs
├── KeywordValidatorObjectTests.cs
├── KeywordValidatorTypeTests.cs
├── NestedPropertyAccessTests.cs
├── NumericValidatorTests.cs
├── OneOfArrayTests.cs
├── SchemaWalkerCoverageTests.cs
└── StringValidatorTests.cs
```

## Test Structure

**Suite Organization:**
```csharp
[TestFixture]
public class ArrayValidatorTests
{
    // ── ValidateMinItems ──────────────────────────────────────────────────

    [Test]
    public void ValidateMinItems_CountAboveMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMinItems(3, 2, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }

    [Test]
    public void ValidateMinItems_CountAtMinimum_ReturnsTrue()
    {
        using var collector = new ErrorCollector();
        ArrayValidator.ValidateMinItems(2, 2, "/tags", collector).Should().BeTrue();
        collector.Count.Should().Be(0);
    }
}
```

**Patterns:**

1. **TestFixture attribute:** Every test class has `[TestFixture]`
2. **Test method naming:** `[MethodName]_[Condition]_[Expected]`
   - Examples:
     - `ValidateMinItems_CountAboveMinimum_ReturnsTrue`
     - `Parse_ValidInput_ReturnsNonNull`
     - `Parse_ByteArray_MalformedJson_ReturnsNull`
3. **Arrange-Act-Assert implied:** Tests use minimal setup; most are inline
4. **Section headers:** Visual separators organize related test groups
   ```csharp
   // ── ValidateMinItems ──────────────────────────────────────────────────
   ```
5. **Single assertion per test:** Most tests verify one condition (FluentAssertions chains)

## Mocking

**Framework:** Not used

**Patterns:**
- No mocks or stubs observed
- Tests create real objects: `new ErrorCollector()`, `JsonContractSchema.Load(...)`
- External state avoided; tests use in-memory test data

**What to Mock:**
- Nothing explicitly; library is designed for unit testing without mocks

**What NOT to Mock:**
- `ErrorCollector`, `ArrayBuffer`, `ParseResult` — test these as-is
- `JsonContractSchema` — real schema loading required for contract tests

## Fixtures and Factories

**Test Data:**
- Helper methods in test classes:
  ```csharp
  private static byte[] SampleJsonBytes => Encoding.UTF8.GetBytes("{\"name\":\"test\"}");

  private static JsonContractSchema CreateSchema() =>
      JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}}}""")!;

  private static JsonContractSchema LoadSchema(string json)
      => JsonContractSchema.Load(json)!;

  private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);
  ```
- Shared constants in test class:
  ```csharp
  private const string SchemaJson = """
      {
          "type": "object",
          "properties": {
              "name": { "type": "string" },
              "age": { "type": "integer" }
          }
      }
      """;
  ```

**Location:**
- Helpers as static private methods in test class: `private static JsonContractSchema CreateSchema()`
- No separate fixture classes
- Constants declared in test class body

**Factory Pattern Example:**
```csharp
[TestFixture]
public class JsonContractSchemaApiTests
{
    private static byte[] SampleJsonBytes =>
        Encoding.UTF8.GetBytes("{\"name\":\"test\"}");

    private static JsonContractSchema CreateSchema() =>
        JsonContractSchema.Load("""{"type":"object","properties":{"name":{"type":"string"}}}""")!;

    [Test]
    public void Parse_ValidInput_ReturnsNonNull()
    {
        var schema = CreateSchema();
        ReadOnlySpan<byte> data = SampleJsonBytes;
        // ...
    }
}
```

## Coverage

**Requirements:**
- Not explicitly configured; no coverage threshold enforced
- Coverlet collector included in test projects for manual coverage runs

**View Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

## Test Types

**Unit Tests:**
- Scope: Individual validators (`ArrayValidator`, `StringValidator`, `TypeValidator`)
- Approach: Direct method calls with `ErrorCollector` parameter
- Example from `ArrayValidatorTests.cs`:
  ```csharp
  [Test]
  public void ValidateMinItems_CountAboveMinimum_ReturnsTrue()
  {
      using var collector = new ErrorCollector();
      ArrayValidator.ValidateMinItems(3, 2, "/tags", collector).Should().BeTrue();
      collector.Count.Should().Be(0);
  }
  ```

**Integration Tests:**
- Scope: Full schema parsing and validation pipeline
- Files: `JsonContractSchemaApiTests.cs`, `JsonSchemaLoadingTests.cs`
- Approach: Load schema from JSON, parse test data, verify errors
- Example from `JsonContractSchemaApiTests.cs`:
  ```csharp
  [Test]
  public void Parse_ByteArray_ValidInput_ReturnsResult()
  {
      var schema = CreateSchema();
      var data = SampleJsonBytes;

      using var result = schema.Parse(data);

      result.Should().NotBeNull();
      result!.Value.IsValid.Should().BeTrue();
      result.Value["/name"].HasValue.Should().BeTrue();
  }
  ```

**Allocation Regression Tests:**
- Scope: Zero-allocation guarantees
- Files: `AllocationTests/` subdirectory
- Approach: Use `GC.GetAllocatedBytesForCurrentThread()` to assert allocation budgets
- Example from `TryParseAllocationTests.cs`:
  ```csharp
  [Test]
  public void TryParse_ByteArray_ZeroAllocationAfterWarmup()
  {
      // Warmup
      var schema = _schema;
      var payload = _payload;
      schema.Parse(payload);

      // Measure
      var before = GC.GetAllocatedBytesForCurrentThread();
      using var result = schema.Parse(payload);
      var after = GC.GetAllocatedBytesForCurrentThread();

      (after - before).Should().BeLessThanOrEqualTo(AllocationBudget);
  }
  ```

**E2E Tests:**
- Not explicitly used; integration tests cover end-to-end scenarios

## Common Patterns

**Async Testing:**
- No async tests observed (library is synchronous)

**Error Testing:**
```csharp
[Test]
public void ValidateMinItems_CountBelowMinimum_ReturnsFalse()
{
    using var collector = new ErrorCollector();
    bool result = ArrayValidator.ValidateMinItems(1, 2, "/tags", collector);

    result.Should().BeFalse();
    collector.Count.Should().Be(1);
    collector[0].Code.Should().Be(ValidationErrorCode.MinItemsExceeded);
    collector[0].Path.Should().Be("/tags");
}
```

**IDisposable Testing:**
```csharp
[Test]
public void Parse_ValidInput_ResultIsValid()
{
    var schema = CreateSchema();
    ReadOnlySpan<byte> data = SampleJsonBytes;

    using var result = schema.Parse(data);

    result.Should().NotBeNull();
    result!.Value.IsValid.Should().BeTrue();
    result.Value.Errors.Count.Should().Be(0);
}
```

**Null/Empty Testing:**
```csharp
[Test]
public void Parse_ByteArray_MalformedJson_ReturnsNull()
{
    var schema = CreateSchema();
    var data = Encoding.UTF8.GetBytes("{bad json");

    var result = schema.Parse(data);

    result.Should().BeNull();
}
```

**Multi-condition Testing:**
```csharp
[Test]
public void TryLoad_WithUnresolvableRef_ReturnsFalse()
{
    var success = JsonContractSchema.TryLoad(
        """{"properties":{"x":{"$ref":"#/$defs/missing"}}}""",
        out var schema);

    success.Should().BeFalse();
    schema.Should().BeNull();
}
```

**Xml String Literals:**
```csharp
[Test]
public void Load_FromString_InvalidJson_ReturnsNull()
{
    var result = JsonContractSchema.Load("""
        {
            "type": "object",
            "properties": {
                "tags": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            }
        }
        """);

    result.Should().NotBeNull();
}
```

## Test Lifecycle

**Setup/Teardown:**
- Tests are standalone; no shared setup observed
- Field initialization per-test (NUnit default): `private JsonContractSchema _schema = null!;`
- `Setup` attributes: Not used; tests initialize inline or via helper methods
- Cleanup via `using` statements: `using var collector = new ErrorCollector();`

**Warmup Pattern:**
Tests perform warmup before measuring allocations:
```csharp
// Warmup
var schema = _schema;
var payload = _payload;
schema.Parse(payload);

// Measure
var before = GC.GetAllocatedBytesForCurrentThread();
using var result = schema.Parse(payload);
var after = GC.GetAllocatedBytesForCurrentThread();
```

## Test Projects Configuration

**Gluey.Contract.Json.Tests.csproj:**
- Location: `tests/Gluey.Contract.Json.Tests/`
- References: `Gluey.Contract.Json` project
- Dependencies: NUnit, FluentAssertions, coverlet, analyzers

**Gluey.Contract.Tests.csproj:**
- Location: `tests/Gluey.Contract.Tests/`
- References: `Gluey.Contract` project
- Same dependencies as above

Both projects:
- Target frameworks: `net9.0;net10.0`
- C# 13, implicit usings, nullable enabled
- Marked as `IsTestProject` and not packable

---

*Testing analysis: 2026-03-18*
