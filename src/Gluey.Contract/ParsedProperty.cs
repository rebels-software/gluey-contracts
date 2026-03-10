using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gluey.Contract;

/// <summary>
/// A zero-allocation accessor into parsed byte data.
/// Holds an offset and length into the original byte buffer.
/// Values are materialized only when accessed via GetString(), GetInt32(), etc.
/// Supports hierarchical access via string indexer (child properties) and
/// int indexer (array elements).
/// </summary>
public readonly struct ParsedProperty
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly string _path;
    private readonly OffsetTable _childTable;
    private readonly Dictionary<string, int>? _childOrdinals;
    private readonly Dictionary<string, ParsedProperty>? _directChildren;
    private readonly ArrayBuffer? _arrayBuffer;
    private readonly int _arrayOrdinal;

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> pointing into the given byte buffer.
    /// Used for leaf (scalar) properties without child navigation.
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
        _childTable = default;
        _childOrdinals = null;
        _directChildren = null;
        _arrayBuffer = null;
        _arrayOrdinal = -1;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> with child-resolution capabilities.
    /// Used for object and array properties that support chained hierarchical access.
    /// </summary>
    /// <param name="buffer">The backing byte buffer containing the raw UTF-8 value.</param>
    /// <param name="offset">The starting offset within the buffer.</param>
    /// <param name="length">The number of bytes for this property's value.</param>
    /// <param name="path">The RFC 6901 JSON Pointer path for this property.</param>
    /// <param name="childTable">The offset table containing child property values.</param>
    /// <param name="childOrdinals">Mapping from child property names to their ordinals in the table.</param>
    /// <param name="arrayBuffer">The array buffer for resolving array element access.</param>
    /// <param name="arrayOrdinal">The ordinal identifying this property's array region (-1 if not an array).</param>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path,
        OffsetTable childTable, Dictionary<string, int>? childOrdinals,
        ArrayBuffer? arrayBuffer, int arrayOrdinal)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
        _path = path;
        _childTable = childTable;
        _childOrdinals = childOrdinals;
        _directChildren = null;
        _arrayBuffer = arrayBuffer;
        _arrayOrdinal = arrayOrdinal;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> with direct child property resolution.
    /// Used for array element objects where child values are not in the shared OffsetTable.
    /// </summary>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path,
        Dictionary<string, ParsedProperty> directChildren)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
        _path = path;
        _childTable = default;
        _childOrdinals = null;
        _directChildren = directChildren;
        _arrayBuffer = null;
        _arrayOrdinal = -1;
    }

    /// <summary>The RFC 6901 JSON Pointer path for this property.</summary>
    public string Path => _path ?? string.Empty;

    /// <summary>Whether this property has a value (was present in the parsed data).</summary>
    public bool HasValue => _length > 0;

    /// <summary>The raw bytes of this property's value.</summary>
    public ReadOnlySpan<byte> RawBytes =>
        _buffer is not null ? _buffer.AsSpan(_offset, _length) : ReadOnlySpan<byte>.Empty;

    /// <summary>
    /// Gets a child property by name. Enables hierarchical access like
    /// <c>result["address"]["street"]</c>.
    /// Returns <see cref="Empty"/> if this property has no children or the name is not found.
    /// </summary>
    /// <param name="name">The child property name to look up.</param>
    public ParsedProperty this[string name]
    {
        get
        {
            if (_directChildren is not null && _directChildren.TryGetValue(name, out var child))
                return child;
            if (_childOrdinals is not null && _childOrdinals.TryGetValue(name, out int ordinal))
                return _childTable[ordinal];
            return Empty;
        }
    }

    /// <summary>
    /// Gets an array element by index. Enables array access like
    /// <c>result["tags"][0]</c>.
    /// Returns <see cref="Empty"/> if this property is not an array or the index is out of bounds.
    /// </summary>
    /// <param name="index">The zero-based element index.</param>
    public ParsedProperty this[int index]
    {
        get
        {
            if (_arrayBuffer is not null && _arrayOrdinal >= 0)
                return _arrayBuffer.Get(_arrayOrdinal, index);
            return Empty;
        }
    }

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
