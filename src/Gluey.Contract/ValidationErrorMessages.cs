namespace Gluey.Contract;

/// <summary>
/// Provides pre-allocated static message strings for each <see cref="ValidationErrorCode"/>.
/// Messages are compile-time constants with no runtime interpolation or context.
/// </summary>
internal static class ValidationErrorMessages
{
    private static readonly string[] Messages;

    static ValidationErrorMessages()
    {
        Messages = new string[(int)ValidationErrorCode.TooManyErrors + 1];

        // None intentionally left null (Get returns string.Empty)

        // Type
        Messages[(int)ValidationErrorCode.TypeMismatch] = "Value does not match the expected type.";

        // Enum/Const
        Messages[(int)ValidationErrorCode.EnumMismatch] = "Value is not one of the allowed enum values.";
        Messages[(int)ValidationErrorCode.ConstMismatch] = "Value does not match the required constant value.";

        // Object keywords
        Messages[(int)ValidationErrorCode.RequiredMissing] = "A required property is missing.";
        Messages[(int)ValidationErrorCode.AdditionalPropertyNotAllowed] = "Additional properties are not allowed.";

        // Array keywords
        Messages[(int)ValidationErrorCode.ItemsInvalid] = "An array item does not match the items schema.";
        Messages[(int)ValidationErrorCode.PrefixItemsInvalid] = "An array item does not match its positional prefixItems schema.";

        // Numeric constraints
        Messages[(int)ValidationErrorCode.MinimumExceeded] = "Value is less than the minimum.";
        Messages[(int)ValidationErrorCode.MaximumExceeded] = "Value is greater than the maximum.";
        Messages[(int)ValidationErrorCode.ExclusiveMinimumExceeded] = "Value is less than or equal to the exclusive minimum.";
        Messages[(int)ValidationErrorCode.ExclusiveMaximumExceeded] = "Value is greater than or equal to the exclusive maximum.";
        Messages[(int)ValidationErrorCode.MultipleOfInvalid] = "Value is not a multiple of the specified divisor.";

        // String constraints
        Messages[(int)ValidationErrorCode.MinLengthExceeded] = "String length is less than the minimum length.";
        Messages[(int)ValidationErrorCode.MaxLengthExceeded] = "String length exceeds the maximum length.";
        Messages[(int)ValidationErrorCode.PatternMismatch] = "String does not match the required pattern.";

        // Array/Object size constraints
        Messages[(int)ValidationErrorCode.MinItemsExceeded] = "Array has fewer items than the minimum.";
        Messages[(int)ValidationErrorCode.MaxItemsExceeded] = "Array has more items than the maximum.";
        Messages[(int)ValidationErrorCode.MinPropertiesExceeded] = "Object has fewer properties than the minimum.";
        Messages[(int)ValidationErrorCode.MaxPropertiesExceeded] = "Object has more properties than the maximum.";

        // Composition keywords
        Messages[(int)ValidationErrorCode.AllOfInvalid] = "Value does not match all of the allOf schemas.";
        Messages[(int)ValidationErrorCode.AnyOfInvalid] = "Value does not match any of the anyOf schemas.";
        Messages[(int)ValidationErrorCode.OneOfInvalid] = "Value does not match exactly one of the oneOf schemas.";
        Messages[(int)ValidationErrorCode.NotInvalid] = "Value matches the not schema when it should not.";

        // Conditional keywords
        Messages[(int)ValidationErrorCode.IfThenInvalid] = "Value matched the if schema but failed the then schema.";
        Messages[(int)ValidationErrorCode.IfElseInvalid] = "Value did not match the if schema and failed the else schema.";
        Messages[(int)ValidationErrorCode.DependentRequiredMissing] = "A dependent required property is missing.";
        Messages[(int)ValidationErrorCode.DependentSchemaInvalid] = "Value does not match the dependent schema.";

        // Advanced keywords
        Messages[(int)ValidationErrorCode.PatternPropertyInvalid] = "A property matching a pattern does not validate against the pattern property schema.";
        Messages[(int)ValidationErrorCode.PropertyNameInvalid] = "A property name does not match the propertyNames schema.";
        Messages[(int)ValidationErrorCode.ContainsInvalid] = "Array does not contain an item matching the contains schema.";
        Messages[(int)ValidationErrorCode.MinContainsExceeded] = "Array contains fewer matching items than required by minContains.";
        Messages[(int)ValidationErrorCode.MaxContainsExceeded] = "Array contains more matching items than allowed by maxContains.";
        Messages[(int)ValidationErrorCode.UniqueItemsViolation] = "Array items are not unique as required by uniqueItems.";

        // Format
        Messages[(int)ValidationErrorCode.FormatInvalid] = "Value does not match the expected format.";

        // Schema reference errors
        Messages[(int)ValidationErrorCode.RefCycle] = "A $ref creates a circular reference chain.";
        Messages[(int)ValidationErrorCode.RefUnresolved] = "A $ref target cannot be resolved.";
        Messages[(int)ValidationErrorCode.AnchorUnresolved] = "A $anchor target cannot be resolved.";
        Messages[(int)ValidationErrorCode.AnchorDuplicate] = "Duplicate $anchor declaration in the same schema resource.";

        // Structural errors
        Messages[(int)ValidationErrorCode.InvalidJson] = "JSON is structurally invalid.";

        // Sentinel
        Messages[(int)ValidationErrorCode.TooManyErrors] = "Too many validation errors; remaining errors have been truncated.";
    }

    /// <summary>
    /// Returns the static message string for the given error code.
    /// Returns <see cref="string.Empty"/> for <see cref="ValidationErrorCode.None"/>.
    /// </summary>
    public static string Get(ValidationErrorCode code) => Messages[(int)code] ?? string.Empty;
}
