// Copyright 2025 Rebels Software sp. z o.o.
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

using System.Buffers;

namespace Gluey.Contract;

/// <summary>
/// ArrayPool-backed storage mapping property ordinals to <see cref="ParsedProperty"/> values.
/// Used by the single-pass walker to record parsed property positions, consumed by ParseResult.
/// </summary>
/// <remarks>
/// The name-to-ordinal mapping belongs to the schema, not to OffsetTable.
/// OffsetTable is purely ordinal-indexed storage. ParseResult bridges string names
/// to ordinals via the schema's lookup table.
/// </remarks>
public readonly struct OffsetTable : IDisposable
{
    private readonly ParsedProperty[]? _entries;
    private readonly int _capacity;

    /// <summary>
    /// Creates a new <see cref="OffsetTable"/> with the specified capacity,
    /// renting a buffer from <see cref="ArrayPool{T}"/>.
    /// </summary>
    /// <param name="capacity">The number of property ordinals this table supports.</param>
    internal OffsetTable(int capacity)
    {
        _capacity = capacity;
        _entries = ArrayPool<ParsedProperty>.Shared.Rent(capacity);
        // Clear the rented region to ensure all slots start as default (Empty)
        Array.Clear(_entries, 0, capacity);
    }

    /// <summary>
    /// Sets a <see cref="ParsedProperty"/> at the given ordinal index.
    /// </summary>
    /// <param name="ordinal">The schema-determined ordinal for the property.</param>
    /// <param name="property">The parsed property value to store.</param>
    /// <remarks>
    /// Writing to the array contents is allowed even in a readonly struct
    /// because the array reference is readonly, not the array contents.
    /// </remarks>
    internal void Set(int ordinal, ParsedProperty property)
    {
        if (_entries is not null && (uint)ordinal < (uint)_capacity)
        {
            _entries[ordinal] = property;
        }
    }

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> at the given ordinal index.
    /// Returns <see cref="ParsedProperty.Empty"/> if the ordinal is out of range or was never set.
    /// </summary>
    /// <param name="ordinal">The schema-determined ordinal for the property.</param>
    public ParsedProperty this[int ordinal]
    {
        get
        {
            if (_entries is not null && (uint)ordinal < (uint)_capacity)
            {
                return _entries[ordinal];
            }

            return ParsedProperty.Empty;
        }
    }

    /// <summary>
    /// The number of property ordinals this table can hold (schema-determined capacity).
    /// Returns 0 for a default (uninitialized) instance.
    /// </summary>
    public int Count => _capacity;

    /// <summary>
    /// Returns the rented buffer to <see cref="ArrayPool{T}"/>, clearing it first.
    /// Safe to call multiple times or on a default instance.
    /// </summary>
    public void Dispose()
    {
        if (_entries is not null)
        {
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
        }
    }
}
