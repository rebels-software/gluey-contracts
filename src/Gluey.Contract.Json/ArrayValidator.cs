using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 array size constraint keywords:
/// minItems, maxItems. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// </summary>
internal static class ArrayValidator
{
    /// <summary>
    /// Validates the "minItems" keyword. Returns true if itemCount &gt;= minItems.
    /// </summary>
    internal static bool ValidateMinItems(int itemCount, int minItems, string path, ErrorCollector collector)
    {
        if (itemCount >= minItems)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinItemsExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "maxItems" keyword. Returns true if itemCount &lt;= maxItems.
    /// </summary>
    internal static bool ValidateMaxItems(int itemCount, int maxItems, string path, ErrorCollector collector)
    {
        if (itemCount <= maxItems)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxItemsExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxItemsExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "contains" keyword with optional minContains/maxContains bounds.
    /// Default effective minimum is 1 when minContains is null.
    /// </summary>
    internal static bool ValidateContains(int matchCount, int? minContains, int? maxContains, string path, ErrorCollector collector)
    {
        int effectiveMin = minContains ?? 1;

        if (matchCount < effectiveMin)
        {
            if (minContains.HasValue)
            {
                collector.Add(new ValidationError(
                    path,
                    ValidationErrorCode.MinContainsExceeded,
                    ValidationErrorMessages.Get(ValidationErrorCode.MinContainsExceeded)));
            }
            else
            {
                collector.Add(new ValidationError(
                    path,
                    ValidationErrorCode.ContainsInvalid,
                    ValidationErrorMessages.Get(ValidationErrorCode.ContainsInvalid)));
            }
            return false;
        }

        if (maxContains.HasValue && matchCount > maxContains.Value)
        {
            collector.Add(new ValidationError(
                path,
                ValidationErrorCode.MaxContainsExceeded,
                ValidationErrorMessages.Get(ValidationErrorCode.MaxContainsExceeded)));
            return false;
        }

        return true;
    }
}
