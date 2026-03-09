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
}
