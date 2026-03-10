namespace Gluey.Contract;

/// <summary>
/// Configuration options that control schema validation behavior.
/// </summary>
public sealed class SchemaOptions
{
    /// <summary>
    /// When <c>true</c>, the <c>format</c> keyword produces validation errors
    /// for values that do not match the declared format.
    /// When <c>false</c> (default), <c>format</c> is treated as an annotation only.
    /// </summary>
    /// <remarks>
    /// Format assertion may allocate (string conversions for .NET parser APIs).
    /// This is a documented exception to the zero-allocation guarantee.
    /// </remarks>
    public bool AssertFormat { get; init; } = false;
}
