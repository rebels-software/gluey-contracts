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

using System.Buffers;

namespace Gluey.Contract;

/// <summary>
/// ArrayPool-backed storage for array element <see cref="ParsedProperty"/> values,
/// keyed by (arrayOrdinal, elementIndex). Each array in the parsed JSON gets a unique
/// ordinal; elements within that array are stored contiguously and accessed by index.
/// </summary>
/// <remarks>
/// ArrayBuffer is a class (not struct) because it is shared by reference across multiple
/// <see cref="ParsedProperty"/> instances and <see cref="ParseResult"/>. Implements
/// <see cref="IDisposable"/> to return the rented array to the pool.
///
/// Uses ArrayPool-backed region tracking instead of Dictionary to avoid heap allocation.
/// Instances are pooled via <see cref="Rent"/> / <see cref="Return"/> to avoid per-call
/// heap allocation on the hot path.
/// </remarks>
internal class ArrayBuffer : IDisposable
{
    [ThreadStatic]
    private static ArrayBuffer? t_cached;

    private ParsedProperty[]? _entries;
    private int _count;
    private int _capacity;

    // Region tracking: ArrayPool-backed arrays instead of Dictionary<int, (int,int)>
    private int[]? _regionStarts;
    private int[]? _regionCounts;
    private int _regionCapacity;

    /// <summary>
    /// Creates a new <see cref="ArrayBuffer"/> with the specified initial capacity.
    /// Prefer <see cref="Rent"/> to avoid heap allocation.
    /// </summary>
    /// <param name="initialCapacity">The initial number of element slots to rent from the pool.</param>
    /// <param name="maxOrdinal">The maximum ordinal that will be used as array key. Used to size region tracking.</param>
    private ArrayBuffer(int initialCapacity, int maxOrdinal)
    {
        _capacity = initialCapacity;
        _entries = ArrayPool<ParsedProperty>.Shared.Rent(initialCapacity);
        _regionCapacity = Math.Max(maxOrdinal, 4);
        _regionStarts = ArrayPool<int>.Shared.Rent(_regionCapacity);
        _regionCounts = ArrayPool<int>.Shared.Rent(_regionCapacity);
        // Initialize region counts to 0 and starts to -1 (unset)
        Array.Fill(_regionStarts, -1, 0, _regionCapacity);
        Array.Clear(_regionCounts, 0, _regionCapacity);
    }

    /// <summary>
    /// Rents an <see cref="ArrayBuffer"/> from the thread-static cache, or creates a new one.
    /// Returns a reset instance ready for use. Call <see cref="Return"/> when done.
    /// </summary>
    /// <param name="initialCapacity">The initial number of element slots.</param>
    /// <param name="maxOrdinal">The maximum ordinal for region tracking.</param>
    internal static ArrayBuffer Rent(int initialCapacity = 16, int maxOrdinal = 16)
    {
        var cached = t_cached;
        if (cached is not null)
        {
            t_cached = null;
            cached.Reset(maxOrdinal);
            return cached;
        }

        return new ArrayBuffer(initialCapacity, maxOrdinal);
    }

    /// <summary>
    /// Returns an <see cref="ArrayBuffer"/> to the thread-static cache for reuse.
    /// The instance's logical state is cleared but ArrayPool arrays are retained.
    /// </summary>
    internal static void Return(ArrayBuffer buffer)
    {
        if (buffer._entries is null)
            return; // Already disposed, don't cache

        // Clear references in entries to avoid holding onto ParsedProperty data
        if (buffer._count > 0)
        {
            Array.Clear(buffer._entries, 0, buffer._count);
        }

        t_cached = buffer;
    }

    /// <summary>
    /// Resets the buffer for reuse, clearing logical state but keeping ArrayPool arrays.
    /// Grows region arrays if the new maxOrdinal requires it.
    /// </summary>
    private void Reset(int maxOrdinal)
    {
        // Clear element data (references), or reallocate if entries were disposed
        if (_entries is not null && _count > 0)
        {
            Array.Clear(_entries, 0, _count);
        }
        else if (_entries is null)
        {
            _entries = ArrayPool<ParsedProperty>.Shared.Rent(_capacity > 0 ? _capacity : 16);
            _capacity = _entries.Length;
        }
        _count = 0;

        int requiredRegionCapacity = Math.Max(maxOrdinal, 4);

        // Grow region arrays if needed
        if (_regionStarts is null || _regionCounts is null || requiredRegionCapacity > _regionCapacity)
        {
            if (_regionStarts is not null)
                ArrayPool<int>.Shared.Return(_regionStarts);
            if (_regionCounts is not null)
                ArrayPool<int>.Shared.Return(_regionCounts);

            _regionCapacity = requiredRegionCapacity;
            _regionStarts = ArrayPool<int>.Shared.Rent(_regionCapacity);
            _regionCounts = ArrayPool<int>.Shared.Rent(_regionCapacity);
        }

        // Reset region tracking
        Array.Fill(_regionStarts!, -1, 0, _regionCapacity);
        Array.Clear(_regionCounts!, 0, _regionCapacity);
    }

    /// <summary>
    /// Adds an array element for the given array ordinal.
    /// Elements for the same ordinal must be added consecutively.
    /// </summary>
    /// <param name="arrayOrdinal">The ordinal identifying which array this element belongs to.</param>
    /// <param name="element">The parsed property representing the array element.</param>
    internal void Add(int arrayOrdinal, ParsedProperty element)
    {
        if (_entries is null || _regionStarts is null || _regionCounts is null)
            return;

        // Grow element array if at capacity
        if (_count >= _capacity)
        {
            int newCapacity = _capacity * 2;
            var newEntries = ArrayPool<ParsedProperty>.Shared.Rent(newCapacity);
            Array.Copy(_entries, newEntries, _count);
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
            _entries = newEntries;
            _capacity = newCapacity;
        }

        // Grow region arrays if ordinal exceeds capacity
        if (arrayOrdinal >= _regionCapacity)
        {
            int newRegionCapacity = Math.Max(_regionCapacity * 2, arrayOrdinal + 1);
            var newStarts = ArrayPool<int>.Shared.Rent(newRegionCapacity);
            var newCounts = ArrayPool<int>.Shared.Rent(newRegionCapacity);
            Array.Copy(_regionStarts, newStarts, _regionCapacity);
            Array.Copy(_regionCounts, newCounts, _regionCapacity);
            Array.Fill(newStarts, -1, _regionCapacity, newRegionCapacity - _regionCapacity);
            Array.Clear(newCounts, _regionCapacity, newRegionCapacity - _regionCapacity);
            ArrayPool<int>.Shared.Return(_regionStarts);
            ArrayPool<int>.Shared.Return(_regionCounts);
            _regionStarts = newStarts;
            _regionCounts = newCounts;
            _regionCapacity = newRegionCapacity;
        }

        // Track region for this array ordinal
        if (_regionStarts[arrayOrdinal] < 0)
        {
            _regionStarts[arrayOrdinal] = _count;
        }

        _entries[_count] = element;
        _count++;
        _regionCounts[arrayOrdinal]++;
    }

    /// <summary>
    /// Gets the array element at the specified index within the given array ordinal's region.
    /// Returns <see cref="ParsedProperty.Empty"/> for invalid ordinal or out-of-bounds index.
    /// </summary>
    /// <param name="arrayOrdinal">The ordinal identifying which array to access.</param>
    /// <param name="elementIndex">The zero-based index within the array.</param>
    internal ParsedProperty Get(int arrayOrdinal, int elementIndex)
    {
        if (_regionStarts is null || _regionCounts is null ||
            arrayOrdinal < 0 || arrayOrdinal >= _regionCapacity)
            return ParsedProperty.Empty;

        int start = _regionStarts[arrayOrdinal];
        int count = _regionCounts[arrayOrdinal];

        if (start < 0 || elementIndex < 0 || elementIndex >= count)
            return ParsedProperty.Empty;

        return _entries![start + elementIndex];
    }

    /// <summary>
    /// Gets the number of elements stored for the given array ordinal.
    /// Returns 0 if the ordinal is not found.
    /// </summary>
    internal int GetCount(int arrayOrdinal)
    {
        if (_regionCounts is not null && arrayOrdinal >= 0 && arrayOrdinal < _regionCapacity)
            return _regionCounts[arrayOrdinal];
        return 0;
    }

    /// <summary>
    /// Returns the instance to the thread-static cache for reuse, or disposes
    /// the rented buffers if the cache is already occupied.
    /// Calling code should use this instead of <see cref="DisposeCore"/> directly.
    /// </summary>
    public void Dispose()
    {
        // Try to return to thread-static cache first
        if (_entries is not null && t_cached is null)
        {
            Return(this);
            return;
        }

        // Cache occupied or already disposed -- release arrays to pool
        DisposeCore();
    }

    /// <summary>
    /// Releases all rented buffers back to <see cref="ArrayPool{T}"/>.
    /// Called when the instance cannot be cached.
    /// </summary>
    private void DisposeCore()
    {
        if (_entries is not null)
        {
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
            _entries = null;
        }
        if (_regionStarts is not null)
        {
            ArrayPool<int>.Shared.Return(_regionStarts);
            _regionStarts = null;
        }
        if (_regionCounts is not null)
        {
            ArrayPool<int>.Shared.Return(_regionCounts);
            _regionCounts = null;
        }
    }
}
