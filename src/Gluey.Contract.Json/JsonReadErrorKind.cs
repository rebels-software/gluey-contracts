namespace Gluey.Contract.Json;

/// <summary>
/// Categorizes structural JSON errors detected during tokenization.
/// </summary>
internal enum JsonReadErrorKind : byte
{
    None = 0,
    InvalidJson,
    UnexpectedEndOfData,
    MaxDepthExceeded,
}
