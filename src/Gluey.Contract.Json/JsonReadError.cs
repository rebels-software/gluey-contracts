namespace Gluey.Contract.Json;

/// <summary>
/// Describes a structural JSON error encountered during tokenization.
/// Separate from schema validation errors (no JSON Pointer path, no schema context).
/// </summary>
internal readonly struct JsonReadError
{
    public JsonReadErrorKind Kind { get; }
    public int ByteOffset { get; }
    public string Message { get; }

    public JsonReadError(JsonReadErrorKind kind, int byteOffset, string message)
    {
        Kind = kind;
        ByteOffset = byteOffset;
        Message = message;
    }
}
