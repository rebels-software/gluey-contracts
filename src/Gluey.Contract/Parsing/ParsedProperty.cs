// Copyright 2026 Rebels Software sp. z o.o.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gluey.Contract;

/// <summary>
/// Byte constants representing contract-declared field types.
/// Used for accessor type strictness validation on binary format properties.
/// </summary>
internal static class FieldTypes
{
    internal const byte None = 0;
    internal const byte UInt8 = 1;
    internal const byte UInt16 = 2;
    internal const byte UInt32 = 3;
    internal const byte Int8 = 4;
    internal const byte Int16 = 5;
    internal const byte Int32 = 6;
    internal const byte Float32 = 7;
    internal const byte Float64 = 8;
    internal const byte Boolean = 9;
    internal const byte String = 10;
    internal const byte Enum = 11;
    internal const byte Bits = 12;
    internal const byte Padding = 13;
}

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
    private readonly byte _format;      // 0 = UTF-8/JSON (default), 1 = Binary
    private readonly byte _endianness;   // 0 = little-endian, 1 = big-endian (only meaningful when _format == 1)
    private readonly byte _fieldType;    // 0 = unset (JSON), 1-13 = binary field type (see FieldTypes)
    private readonly byte _encoding;     // bits 0: 0=UTF-8, 1=ASCII; bits 2-3: trim mode (0=plain, 1=trimStart, 2=trimEnd, 3=trim)
    private readonly Dictionary<string, string>? _enumValues; // Enum value mapping for lazy GetString() lookup

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
        _format = 0;
        _endianness = 0;
        _fieldType = 0;
        _encoding = 0;
        _enumValues = null;
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
        _format = 0;
        _endianness = 0;
        _fieldType = 0;
        _encoding = 0;
        _enumValues = null;
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
        _format = 0;
        _endianness = 0;
        _fieldType = 0;
        _encoding = 0;
        _enumValues = null;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> for binary format leaf/scalar properties with field type metadata.
    /// </summary>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness, byte fieldType)
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
        _format = format;
        _endianness = endianness;
        _fieldType = fieldType;
        _encoding = 0;
        _enumValues = null;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> for binary string fields with encoding and trim mode metadata.
    /// </summary>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness, byte fieldType, byte encoding)
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
        _format = format;
        _endianness = endianness;
        _fieldType = fieldType;
        _encoding = encoding;
        _enumValues = null;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> for binary enum label fields with lazy dictionary lookup.
    /// </summary>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness, byte fieldType, Dictionary<string, string>? enumValues)
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
        _format = format;
        _endianness = endianness;
        _fieldType = fieldType;
        _encoding = 0;
        _enumValues = enumValues;
    }

    /// <summary>
    /// Creates a new <see cref="ParsedProperty"/> for binary format properties with child navigation and field type metadata.
    /// </summary>
    internal ParsedProperty(byte[] buffer, int offset, int length, string path, byte format, byte endianness, byte fieldType,
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
        _format = format;
        _endianness = endianness;
        _fieldType = fieldType;
        _encoding = 0;
        _enumValues = null;
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
            if (_childOrdinals is not null)
            {
                if (_childOrdinals.TryGetValue(name, out int ordinal))
                    return _childTable[ordinal];
                // Prefix-based lookup: use this property's path to scope child resolution.
                // For struct array elements like "/errors/0", looking up "code" resolves to "errors/0/code".
                if (_path is not null && _path.Length > 1)
                {
                    var prefixedKey = _path.StartsWith('/')
                        ? _path.Substring(1) + "/" + name
                        : _path + "/" + name;
                    if (_childOrdinals.TryGetValue(prefixedKey, out ordinal))
                        return _childTable[ordinal];
                }
                // Fallback: _childOrdinals keys are full RFC 6901 paths (e.g., "/address/street").
                // When user calls prop["street"], find the key ending in "/street".
                foreach (var kvp in _childOrdinals)
                {
                    if (kvp.Key.Length > 0 && kvp.Key.EndsWith("/" + name, StringComparison.Ordinal))
                        return _childTable[kvp.Value];
                }
            }
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
        if (_format == 0)
            return Encoding.UTF8.GetString(_buffer, _offset, _length);
        // Binary path
        if (_fieldType == FieldTypes.Enum)
        {
            string key = _buffer[_offset].ToString();
            if (_enumValues is not null && _enumValues.TryGetValue(key, out string? label))
                return label;
            return key; // unmapped or no values table: return numeric as string per D-08
        }
        if (_fieldType == FieldTypes.String || _fieldType == FieldTypes.None)
        {
            var span = _buffer.AsSpan(_offset, _length);
            // Apply trim mode: bits 2-3 of _encoding (0=plain, 1=trimStart, 2=trimEnd, 3=trim)
            byte mode = (byte)((_encoding >> 2) & 0x03);
            if (mode == 1 || mode == 3) // trimStart or trim
                span = span.TrimStart((byte)0);
            if (mode == 2 || mode == 3) // trimEnd or trim
            {
                // Trim trailing null bytes and ASCII whitespace (0x00, 0x20, 0x09, 0x0A, 0x0D)
                int end = span.Length;
                while (end > 0 && (span[end - 1] == 0x00 || span[end - 1] == 0x20 || span[end - 1] == 0x09 || span[end - 1] == 0x0A || span[end - 1] == 0x0D))
                    end--;
                span = span.Slice(0, end);
            }
            // Decode with appropriate encoding
            bool isAscii = (_encoding & 0x01) == 1;
            return isAscii
                ? Encoding.ASCII.GetString(span)
                : Encoding.UTF8.GetString(span);
        }
        // Non-string, non-enum binary field: type strictness
        throw new InvalidOperationException($"Cannot read String from field of type '{GetFieldTypeName()}' at path '{_path}'.");
    }

    /// <summary>
    /// Materializes the value as an unsigned 8-bit integer from binary data.
    /// Returns <c>default(byte)</c> when <see cref="HasValue"/> is false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetUInt8()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.UInt8)
            throw new InvalidOperationException($"Cannot read UInt8 from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
            return (byte)value;
        }
        return _buffer[_offset];
    }

    /// <summary>
    /// Materializes the value as an unsigned 16-bit integer from binary data.
    /// Returns <c>default(ushort)</c> when <see cref="HasValue"/> is false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetUInt16()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.UInt16)
            throw new InvalidOperationException($"Cannot read UInt16 from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
            return (ushort)value;
        }
        var span = _buffer.AsSpan(_offset, _length);
        if (_endianness == 0)
        {
            return _length switch
            {
                1 => span[0],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
                _ => throw new InvalidOperationException($"Cannot read UInt16 from {_length} bytes at path '{_path}'")
            };
        }
        return _length switch
        {
            1 => span[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(span),
            _ => throw new InvalidOperationException($"Cannot read UInt16 from {_length} bytes at path '{_path}'")
        };
    }

    /// <summary>
    /// Materializes the value as an unsigned 32-bit integer from binary data.
    /// Returns <c>default(uint)</c> when <see cref="HasValue"/> is false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetUInt32()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.UInt32)
            throw new InvalidOperationException($"Cannot read UInt32 from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out uint value, out _);
            return value;
        }
        var span = _buffer.AsSpan(_offset, _length);
        if (_endianness == 0)
        {
            return _length switch
            {
                1 => span[0],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(span),
                3 => (uint)span[0] | ((uint)span[1] << 8) | ((uint)span[2] << 16),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(span),
                _ => throw new InvalidOperationException($"Cannot read UInt32 from {_length} bytes at path '{_path}'")
            };
        }
        return _length switch
        {
            1 => span[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(span),
            3 => ((uint)span[0] << 16) | ((uint)span[1] << 8) | span[2],
            4 => BinaryPrimitives.ReadUInt32BigEndian(span),
            _ => throw new InvalidOperationException($"Cannot read UInt32 from {_length} bytes at path '{_path}'")
        };
    }

    /// <summary>
    /// Materializes the value as a 32-bit integer parsed from UTF-8 bytes.
    /// Returns <c>default(int)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.Int32)
            throw new InvalidOperationException($"Cannot read Int32 from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
            return value;
        }
        var span = _buffer.AsSpan(_offset, _length);
        if (_endianness == 0)
        {
            return _length switch
            {
                1 => (sbyte)span[0],
                2 => BinaryPrimitives.ReadInt16LittleEndian(span),
                3 => SignExtend3BytesLittleEndian(span),
                4 => BinaryPrimitives.ReadInt32LittleEndian(span),
                _ => throw new InvalidOperationException($"Cannot read Int32 from {_length} bytes at path '{_path}'")
            };
        }
        return _length switch
        {
            1 => (sbyte)span[0],
            2 => BinaryPrimitives.ReadInt16BigEndian(span),
            3 => SignExtend3BytesBigEndian(span),
            4 => BinaryPrimitives.ReadInt32BigEndian(span),
            _ => throw new InvalidOperationException($"Cannot read Int32 from {_length} bytes at path '{_path}'")
        };
    }

    /// <summary>
    /// Materializes the value as a 64-bit integer parsed from UTF-8 bytes.
    /// Returns <c>default(long)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInt64()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.Int32 && _fieldType != FieldTypes.Int16 && _fieldType != FieldTypes.Int8)
            throw new InvalidOperationException($"Cannot read Int64 from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out long value, out _);
            return value;
        }
        var span = _buffer.AsSpan(_offset, _length);
        if (_endianness == 0)
        {
            return _length switch
            {
                1 => (sbyte)span[0],
                2 => BinaryPrimitives.ReadInt16LittleEndian(span),
                3 => SignExtend3BytesLittleEndian(span),
                4 => BinaryPrimitives.ReadInt32LittleEndian(span),
                8 => BinaryPrimitives.ReadInt64LittleEndian(span),
                _ => throw new InvalidOperationException($"Cannot read Int64 from {_length} bytes at path '{_path}'")
            };
        }
        return _length switch
        {
            1 => (sbyte)span[0],
            2 => BinaryPrimitives.ReadInt16BigEndian(span),
            3 => SignExtend3BytesBigEndian(span),
            4 => BinaryPrimitives.ReadInt32BigEndian(span),
            8 => BinaryPrimitives.ReadInt64BigEndian(span),
            _ => throw new InvalidOperationException($"Cannot read Int64 from {_length} bytes at path '{_path}'")
        };
    }

    /// <summary>
    /// Materializes the value as a double parsed from UTF-8 bytes.
    /// Returns <c>default(double)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble()
    {
        if (_length == 0) return default;
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.Float64 && _fieldType != FieldTypes.Float32)
            throw new InvalidOperationException($"Cannot read Double from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out double value, out _);
            return value;
        }
        var span = _buffer.AsSpan(_offset, _length);
        if (_endianness == 0)
        {
            return _length switch
            {
                4 => BinaryPrimitives.ReadSingleLittleEndian(span),
                8 => BinaryPrimitives.ReadDoubleLittleEndian(span),
                _ => throw new InvalidOperationException($"Cannot read Double from {_length} bytes at path '{_path}'")
            };
        }
        return _length switch
        {
            4 => BinaryPrimitives.ReadSingleBigEndian(span),
            8 => BinaryPrimitives.ReadDoubleBigEndian(span),
            _ => throw new InvalidOperationException($"Cannot read Double from {_length} bytes at path '{_path}'")
        };
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
        if (_format == 1 && _fieldType != FieldTypes.None && _fieldType != FieldTypes.Boolean)
            throw new InvalidOperationException($"Cannot read Boolean from field of type '{GetFieldTypeName()}' at path '{_path}'.");
        if (_format == 0)
            return _length == 4 && _buffer[_offset] == (byte)'t';
        return _buffer[_offset] != 0;
    }

    /// <summary>
    /// Materializes the value as a decimal parsed from UTF-8 bytes.
    /// Returns <c>default(decimal)</c> when <see cref="HasValue"/> is false or the bytes cannot be parsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal GetDecimal()
    {
        if (_length == 0) return default;
        if (_format == 0)
        {
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out decimal value, out _);
            return value;
        }
        throw new NotSupportedException($"Binary format does not support decimal type at path '{_path}'");
    }

    /// <summary>
    /// Gets the number of elements in this array property.
    /// Returns 0 if this property is not an array.
    /// </summary>
    public int Count =>
        _arrayBuffer is not null && _arrayOrdinal >= 0
            ? _arrayBuffer.GetCount(_arrayOrdinal)
            : 0;

    /// <summary>
    /// Returns a zero-allocation enumerator over array elements,
    /// enabling <c>foreach (var elem in property)</c> for array properties.
    /// For non-array properties, the enumerator yields zero elements.
    /// </summary>
    public ArrayEnumerator GetEnumerator()
    {
        if (_arrayBuffer is not null && _arrayOrdinal >= 0)
            return new ArrayEnumerator(_arrayBuffer, _arrayOrdinal, _arrayBuffer.GetCount(_arrayOrdinal));
        return default;
    }

    /// <summary>Returns a default <see cref="ParsedProperty"/> with no value.</summary>
    public static ParsedProperty Empty => default;

    // -- Private helpers --

    private static int SignExtend3BytesBigEndian(ReadOnlySpan<byte> span)
    {
        byte fill = (span[0] & 0x80) != 0 ? (byte)0xFF : (byte)0x00;
        return (fill << 24) | (span[0] << 16) | (span[1] << 8) | span[2];
    }

    private static int SignExtend3BytesLittleEndian(ReadOnlySpan<byte> span)
    {
        byte fill = (span[2] & 0x80) != 0 ? (byte)0xFF : (byte)0x00;
        return (fill << 24) | (span[2] << 16) | (span[1] << 8) | span[0];
    }

    private string GetFieldTypeName() => _fieldType switch
    {
        FieldTypes.UInt8 => "uint8",
        FieldTypes.UInt16 => "uint16",
        FieldTypes.UInt32 => "uint32",
        FieldTypes.Int8 => "int8",
        FieldTypes.Int16 => "int16",
        FieldTypes.Int32 => "int32",
        FieldTypes.Float32 => "float32",
        FieldTypes.Float64 => "float64",
        FieldTypes.Boolean => "boolean",
        FieldTypes.String => "string",
        FieldTypes.Enum => "enum",
        _ => _fieldType.ToString()
    };

    /// <summary>
    /// A stack-allocated enumerator over array elements of a <see cref="ParsedProperty"/>.
    /// Follows the zero-allocation duck-typed enumerator pattern (no IEnumerator interface).
    /// </summary>
    public struct ArrayEnumerator
    {
        private readonly ArrayBuffer _buffer;
        private readonly int _arrayOrdinal;
        private readonly int _count;
        private int _index;

        internal ArrayEnumerator(ArrayBuffer buffer, int arrayOrdinal, int count)
        {
            _buffer = buffer;
            _arrayOrdinal = arrayOrdinal;
            _count = count;
            _index = -1;
        }

        /// <summary>Gets the current array element.</summary>
        public ParsedProperty Current => _buffer.Get(_arrayOrdinal, _index);

        /// <summary>Advances the enumerator to the next array element.</summary>
        public bool MoveNext() => ++_index < _count;
    }
}
