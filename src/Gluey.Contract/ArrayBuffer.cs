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
/// </remarks>
internal class ArrayBuffer : IDisposable
{
    private ParsedProperty[]? _entries;
    private int _count;
    private int _capacity;
    private Dictionary<int, (int Start, int Count)>? _regions;

    /// <summary>
    /// Creates a new <see cref="ArrayBuffer"/> with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial number of element slots to rent from the pool.</param>
    internal ArrayBuffer(int initialCapacity = 16)
    {
        _capacity = initialCapacity;
        _entries = ArrayPool<ParsedProperty>.Shared.Rent(initialCapacity);
        _regions = new Dictionary<int, (int Start, int Count)>();
    }

    /// <summary>
    /// Adds an array element for the given array ordinal.
    /// Elements for the same ordinal must be added consecutively.
    /// </summary>
    /// <param name="arrayOrdinal">The ordinal identifying which array this element belongs to.</param>
    /// <param name="element">The parsed property representing the array element.</param>
    internal void Add(int arrayOrdinal, ParsedProperty element)
    {
        if (_entries is null || _regions is null)
            return;

        // Grow if at capacity
        if (_count >= _capacity)
        {
            int newCapacity = _capacity * 2;
            var newEntries = ArrayPool<ParsedProperty>.Shared.Rent(newCapacity);
            Array.Copy(_entries, newEntries, _count);
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
            _entries = newEntries;
            _capacity = newCapacity;
        }

        // Track region for this array ordinal
        if (!_regions.TryGetValue(arrayOrdinal, out var region))
        {
            region = (_count, 0);
        }

        _entries[_count] = element;
        _count++;
        _regions[arrayOrdinal] = (region.Start, region.Count + 1);
    }

    /// <summary>
    /// Gets the array element at the specified index within the given array ordinal's region.
    /// Returns <see cref="ParsedProperty.Empty"/> for invalid ordinal or out-of-bounds index.
    /// </summary>
    /// <param name="arrayOrdinal">The ordinal identifying which array to access.</param>
    /// <param name="elementIndex">The zero-based index within the array.</param>
    internal ParsedProperty Get(int arrayOrdinal, int elementIndex)
    {
        if (_regions is null || !_regions.TryGetValue(arrayOrdinal, out var region))
            return ParsedProperty.Empty;

        if (elementIndex < 0 || elementIndex >= region.Count)
            return ParsedProperty.Empty;

        return _entries![region.Start + elementIndex];
    }

    /// <summary>
    /// Gets the number of elements stored for the given array ordinal.
    /// Returns 0 if the ordinal is not found.
    /// </summary>
    internal int GetCount(int arrayOrdinal)
    {
        if (_regions is not null && _regions.TryGetValue(arrayOrdinal, out var region))
            return region.Count;
        return 0;
    }

    /// <summary>
    /// Returns the rented buffer to <see cref="ArrayPool{T}"/>.
    /// Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_entries is not null)
        {
            ArrayPool<ParsedProperty>.Shared.Return(_entries, clearArray: true);
            _entries = null;
        }
        _regions = null;
    }
}
