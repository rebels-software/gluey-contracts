using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Static validator methods for JSON Schema Draft 2020-12 object size constraint keywords:
/// minProperties, maxProperties. Each method returns true on success, false on failure
/// (after pushing error to the collector).
/// </summary>
internal static class ObjectValidator
{
    /// <summary>
    /// Validates the "minProperties" keyword. Returns true if propertyCount &gt;= minProperties.
    /// </summary>
    internal static bool ValidateMinProperties(int propertyCount, int minProperties, string path, ErrorCollector collector)
    {
        if (propertyCount >= minProperties)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MinPropertiesExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MinPropertiesExceeded)));
        return false;
    }

    /// <summary>
    /// Validates the "maxProperties" keyword. Returns true if propertyCount &lt;= maxProperties.
    /// </summary>
    internal static bool ValidateMaxProperties(int propertyCount, int maxProperties, string path, ErrorCollector collector)
    {
        if (propertyCount <= maxProperties)
            return true;

        collector.Add(new ValidationError(
            path,
            ValidationErrorCode.MaxPropertiesExceeded,
            ValidationErrorMessages.Get(ValidationErrorCode.MaxPropertiesExceeded)));
        return false;
    }

    /// <summary>
    /// Validates a property value against its matching patternProperties subschema.
    /// Returns true if the subschema result passed.
    /// </summary>
    internal static bool ValidatePatternProperty(bool schemaResult, string propertyName, string path, ErrorCollector collector)
    {
        if (schemaResult)
            return true;

        collector.Add(new ValidationError(
            SchemaNode.BuildChildPath(path, propertyName),
            ValidationErrorCode.PatternPropertyInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.PatternPropertyInvalid)));
        return false;
    }

    /// <summary>
    /// Validates a property name against the propertyNames subschema.
    /// Returns true if the name schema result passed.
    /// </summary>
    internal static bool ValidatePropertyName(bool nameSchemaResult, string propertyName, string path, ErrorCollector collector)
    {
        if (nameSchemaResult)
            return true;

        collector.Add(new ValidationError(
            SchemaNode.BuildChildPath(path, propertyName),
            ValidationErrorCode.PropertyNameInvalid,
            ValidationErrorMessages.Get(ValidationErrorCode.PropertyNameInvalid)));
        return false;
    }
}
