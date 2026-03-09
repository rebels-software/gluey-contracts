using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 conditional keywords:
/// if/then/else. Each method returns true on success, false on failure
/// (after pushing an error to the collector).
/// These validators receive pre-computed boolean results from the walker and apply
/// conditional logic only. The walker decides when to call each method based on
/// if-schema evaluation results and presence of then/else schemas.
/// </summary>
internal static class ConditionalValidator
{
    /// <summary>
    /// Validates the "then" branch of an if/then conditional.
    /// Called by the walker only when the if-schema passed and a then-schema exists.
    /// Returns true if the then-schema passed; otherwise pushes <see cref="ValidationErrorCode.IfThenInvalid"/>.
    /// </summary>
    internal static bool ValidateIfThen(bool thenResult, string path, ErrorCollector collector)
    {
        if (thenResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.IfThenInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfThenInvalid)));
        return false;
    }

    /// <summary>
    /// Validates the "else" branch of an if/then/else conditional.
    /// Called by the walker only when the if-schema failed and an else-schema exists.
    /// Returns true if the else-schema passed; otherwise pushes <see cref="ValidationErrorCode.IfElseInvalid"/>.
    /// </summary>
    internal static bool ValidateIfElse(bool elseResult, string path, ErrorCollector collector)
    {
        if (elseResult)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.IfElseInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.IfElseInvalid)));
        return false;
    }
}
