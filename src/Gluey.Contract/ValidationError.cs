namespace Gluey.Contract;

/// <summary>
/// A validation error with an RFC 6901 JSON Pointer path, machine-readable error code, and human-readable message.
/// </summary>
public readonly struct ValidationError
{
    /// <summary>RFC 6901 JSON Pointer path to the failing property.</summary>
    public readonly string Path;

    /// <summary>Machine-readable error code identifying the validation failure.</summary>
    public readonly ValidationErrorCode Code;

    /// <summary>Human-readable static error message describing the failure.</summary>
    public readonly string Message;

    /// <summary>
    /// Creates a new <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="path">RFC 6901 JSON Pointer path to the failing property.</param>
    /// <param name="code">Machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    public ValidationError(string path, ValidationErrorCode code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }
}
