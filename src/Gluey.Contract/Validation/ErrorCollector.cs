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
using System.Collections;

namespace Gluey.Contract;

/// <summary>
/// Pre-allocated, ArrayPool-backed buffer for collecting <see cref="ValidationError"/> instances
/// without heap allocation during the parse/validation path.
/// </summary>
/// <remarks>
/// When the buffer reaches capacity, the last slot is replaced with a
/// <see cref="ValidationErrorCode.TooManyErrors"/> sentinel error, and any further
/// errors are silently dropped. The capacity is configurable per schema (default 64).
/// </remarks>
public readonly struct ErrorCollector : IDisposable
{
    /// <summary>Default maximum number of errors to collect.</summary>
    internal const int DefaultCapacity = 64;

    private readonly ValidationError[]? _errors;
    private readonly int[]? _countHolder;
    private readonly int _capacity;

    /// <summary>
    /// Creates a new <see cref="ErrorCollector"/> with the default capacity (64),
    /// renting a buffer from <see cref="ArrayPool{T}"/>.
    /// </summary>
    public ErrorCollector()
        : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Creates a new <see cref="ErrorCollector"/> with the specified capacity,
    /// renting a buffer from <see cref="ArrayPool{T}"/>.
    /// </summary>
    /// <param name="capacity">Maximum number of errors to collect before sentinel overflow.</param>
    internal ErrorCollector(int capacity)
    {
        _capacity = capacity;
        _errors = ArrayPool<ValidationError>.Shared.Rent(capacity);
        _countHolder = ArrayPool<int>.Shared.Rent(1);
        _countHolder[0] = 0;
        Array.Clear(_errors, 0, capacity);
    }

    /// <summary>
    /// Adds a <see cref="ValidationError"/> to the collector.
    /// When the buffer is full, the last slot is replaced with a TooManyErrors sentinel
    /// and further errors are silently dropped.
    /// </summary>
    /// <param name="error">The validation error to add.</param>
    public void Add(ValidationError error)
    {
        if (_errors is null || _countHolder is null)
            return;

        int count = _countHolder[0];

        if (count >= _capacity)
            return; // Already full (sentinel placed), silently drop

        if (count == _capacity - 1)
        {
            // Last slot: replace with sentinel
            _errors[count] = new ValidationError(
                string.Empty,
                ValidationErrorCode.TooManyErrors,
                ValidationErrorMessages.Get(ValidationErrorCode.TooManyErrors));
            _countHolder[0] = _capacity;
            return;
        }

        _errors[count] = error;
        _countHolder[0] = count + 1;
    }

    /// <summary>
    /// Gets the <see cref="ValidationError"/> at the specified index.
    /// </summary>
    /// <param name="index">Zero-based index of the error.</param>
    public ValidationError this[int index]
    {
        get
        {
            if (_errors is not null && _countHolder is not null
                && (uint)index < (uint)_countHolder[0])
            {
                return _errors[index];
            }

            return default;
        }
    }

    /// <summary>
    /// The number of errors currently collected (including sentinel if present).
    /// Returns 0 for a default (uninitialized) instance.
    /// </summary>
    public int Count => _countHolder is not null ? _countHolder[0] : 0;

    /// <summary>
    /// Whether any errors have been collected.
    /// </summary>
    public bool HasErrors => Count > 0;

    /// <summary>
    /// Returns a struct enumerator over the collected errors, enabling foreach support.
    /// </summary>
    public Enumerator GetEnumerator() => new Enumerator(_errors, Count);

    /// <summary>
    /// Returns the rented buffers to <see cref="ArrayPool{T}"/>, clearing error buffer first.
    /// Safe to call multiple times or on a default instance.
    /// </summary>
    public void Dispose()
    {
        if (_errors is not null)
        {
            ArrayPool<ValidationError>.Shared.Return(_errors, clearArray: true);
        }
        if (_countHolder is not null)
        {
            ArrayPool<int>.Shared.Return(_countHolder);
        }
    }

    /// <summary>
    /// A stack-allocated enumerator over collected <see cref="ValidationError"/> entries.
    /// </summary>
    public struct Enumerator
    {
        private readonly ValidationError[]? _errors;
        private readonly int _count;
        private int _index;

        internal Enumerator(ValidationError[]? errors, int count)
        {
            _errors = errors;
            _count = count;
            _index = -1;
        }

        /// <summary>Gets the current <see cref="ValidationError"/>.</summary>
        public ValidationError Current => _errors![_index];

        /// <summary>Advances the enumerator to the next element.</summary>
        public bool MoveNext()
        {
            _index++;
            return _index < _count;
        }
    }
}
