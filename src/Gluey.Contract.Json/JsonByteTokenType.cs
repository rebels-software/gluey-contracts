namespace Gluey.Contract.Json;

/// <summary>
/// Identifies the type of a JSON token produced by <see cref="JsonByteReader"/>.
/// Decoupled from <see cref="System.Text.Json.JsonTokenType"/> to avoid BCL coupling.
/// </summary>
internal enum JsonByteTokenType : byte
{
    None = 0,
    StartObject,
    EndObject,
    StartArray,
    EndArray,
    PropertyName,
    String,
    Number,
    True,
    False,
    Null,
}
