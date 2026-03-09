using System.Text.Json;

namespace Gluey.Contract.Json;

/// <summary>
/// Forward-only, zero-allocation JSON tokenizer that wraps <see cref="Utf8JsonReader"/>
/// and reports per-token type, byte offset, and byte length.
/// String and PropertyName offsets point to content inside quotes,
/// matching the <see cref="Gluey.Contract.ParsedProperty"/> contract.
/// </summary>
internal ref struct JsonByteReader
{
    private Utf8JsonReader _reader;
    private JsonReadError _error;
    private readonly int _inputLength;

    public JsonByteReader(ReadOnlySpan<byte> utf8Json)
    {
        _reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        _error = default;
        _inputLength = utf8Json.Length;
        TokenType = JsonByteTokenType.None;
        ByteOffset = 0;
        ByteLength = 0;
    }

    public JsonByteTokenType TokenType { get; private set; }
    public int ByteOffset { get; private set; }
    public int ByteLength { get; private set; }
    public JsonReadError Error => _error;
    public bool HasError => _error.Kind != JsonReadErrorKind.None;

    public bool Read()
    {
        try
        {
            if (!_reader.Read())
                return false;

            TokenType = MapTokenType(_reader.TokenType);
            ComputeOffsets();
            return true;
        }
        catch (JsonException ex)
        {
            var kind = _reader.BytesConsumed >= _inputLength
                ? JsonReadErrorKind.UnexpectedEndOfData
                : JsonReadErrorKind.InvalidJson;

            _error = new JsonReadError(kind, (int)_reader.BytesConsumed, ex.Message);
            return false;
        }
    }

    private void ComputeOffsets()
    {
        if (TokenType == JsonByteTokenType.String || TokenType == JsonByteTokenType.PropertyName)
        {
            ByteOffset = (int)_reader.TokenStartIndex + 1;
            ByteLength = _reader.ValueSpan.Length;
        }
        else
        {
            ByteOffset = (int)_reader.TokenStartIndex;
            ByteLength = _reader.ValueSpan.Length;
        }
    }

    private static JsonByteTokenType MapTokenType(JsonTokenType tokenType) => tokenType switch
    {
        JsonTokenType.StartObject => JsonByteTokenType.StartObject,
        JsonTokenType.EndObject => JsonByteTokenType.EndObject,
        JsonTokenType.StartArray => JsonByteTokenType.StartArray,
        JsonTokenType.EndArray => JsonByteTokenType.EndArray,
        JsonTokenType.PropertyName => JsonByteTokenType.PropertyName,
        JsonTokenType.String => JsonByteTokenType.String,
        JsonTokenType.Number => JsonByteTokenType.Number,
        JsonTokenType.True => JsonByteTokenType.True,
        JsonTokenType.False => JsonByteTokenType.False,
        JsonTokenType.Null => JsonByteTokenType.Null,
        JsonTokenType.Comment => JsonByteTokenType.None,
        _ => JsonByteTokenType.None,
    };
}
