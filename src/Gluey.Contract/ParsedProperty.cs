using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gluey.Contract;

/// <summary>
/// A zero-allocation accessor into parsed byte data.
/// Holds an offset and length into the original byte buffer.
/// Values are materialized only when accessed via GetString(), GetInt32(), etc.
/// </summary>
public readonly struct ParsedProperty
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> pointing into the given byte buffer.
    /// </summary>
    /// <param name="buffer">The backing byte buffer containing the raw UTF-8 value.</param>
    /// <param name="offset">The starting offset within the buffer.</param>
    /// <param name="length">The number of bytes for this property's value.</param>
    /// <param name="path">The RFC 6901 JSON Pointer path for this property.</param>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
        _path = path;
    }

    /// <summary>The RFC 6901 JSON Pointer path for this property.</summary>
    public string Path => _path ?? string.Empty;

    /// <summary>Whether this property has a value (was present in the parsed data).</summary>
    public bool HasValue => _length > 0;

    /// <summary>The raw bytes of this property's value.</summary>
    public ReadOnlySpan<byte> RawBytes =>
        _buffer is not null ? _buffer.AsSpan(_offset, _length) : ReadOnlySpan<byte>.Empty;

    /// <summary>
    /// Materializes the value as a UTF-8 decoded string.
    /// Returns <see cref="string.Empty"/> when <see cref="HasValue"/> is false.
    /// </summary>
    /// <remarks>This method allocates a new string on each call (by design -- outside the parse path).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString()
    {
        if (_length == 0) return string.Empty;
        return Encoding.UTF8.GetString(_buffer, _offset, _length);
    }

    /// <summary>
    /// Materializes the value as a 32-bit integer parsed from UTF-8 bytes.
    /// Returns <c>default(int)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
        return value;
    }

    /// <summary>
    /// Materializes the value as a 64-bit integer parsed from UTF-8 bytes.
    /// Returns <c>default(long)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInt64()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out long value, out _);
        return value;
    }

    /// <summary>
    /// Materializes the value as a double parsed from UTF-8 bytes.
    /// Returns <c>default(double)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out double value, out _);
        return value;
    }

    /// <summary>
    /// Materializes the value as a boolean from UTF-8 bytes.
    /// JSON "true" (4 bytes starting with 't') returns <c>true</c>; all other values return <c>false</c>.
    /// Returns <c>default(bool)</c> when <see cref="HasValue"/> is false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean()
    {
        if (_length == 0) return default;
        return _length == 4 && _buffer[_offset] == (byte)'t';
    }

    /// <summary>
    /// Materializes the value as a decimal parsed from UTF-8 bytes.
    /// Returns <c>default(decimal)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal GetDecimal()
    {
        if (_length == 0) return default;
        Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out decimal value, out _);
        return value;
    }

    /// <summary>Returns a default <see cref="ParsedProperty"/> with no value.</summary>
    public static ParsedProperty Empty => default;
}
