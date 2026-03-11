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

namespace Gluey.Contract;

/// <summary>
/// The composite result of parsing and validating raw bytes against a schema.
/// Wraps an <see cref="OffsetTable"/> for property access and an <see cref="ErrorCollector"/>
/// for validation errors. Supports both ordinal and string-based indexing.
/// </summary>
/// <remarks>
/// ParseResult is the public-facing return type from <c>JsonContractSchema.TryParse</c>
/// and <c>JsonContractSchema.Parse</c>. It ties together all parsed property data with
/// any validation errors found during the single-pass walk.
///
/// Both indexers return <see cref="ParsedProperty.Empty"/> for missing or out-of-range
/// properties, never throwing exceptions. Implements <see cref="IDisposable"/> to cascade
/// disposal to the underlying <see cref="OffsetTable"/> and <see cref="ErrorCollector"/>,
/// returning their ArrayPool buffers.
/// </remarks>
public readonly struct ParseResult : IDisposable
{
    private readonly OffsetTable _offsetTable;
    private readonly ErrorCollector _errorCollector;
    private readonly Dictionary<string, int>? _nameToOrdinal;
    private readonly ArrayBuffer? _arrayBuffer;

    /// <summary>
    /// Creates a new <see cref="ParseResult"/> wrapping the given offset table, error collector,
    /// and name-to-ordinal mapping from the schema.
    /// </summary>
    /// <param name="offsetTable">The parsed property storage.</param>
    /// <param name="errorCollector">The collected validation errors.</param>
    /// <param name="nameToOrdinal">
    /// The schema's mapping from property names to ordinal indices.
    /// Enables the string indexer (<c>result["name"]</c>).
    /// </param>
    internal ParseResult(OffsetTable offsetTable, ErrorCollector errorCollector, Dictionary<string, int> nameToOrdinal)
    {
        _offsetTable = offsetTable;
        _errorCollector = errorCollector;
        _nameToOrdinal = nameToOrdinal;
        _arrayBuffer = null;
    }

    /// <summary>
    /// Creates a new <see cref="ParseResult"/> wrapping the given offset table, error collector,
    /// name-to-ordinal mapping, and array buffer.
    /// </summary>
    /// <param name="offsetTable">The parsed property storage.</param>
    /// <param name="errorCollector">The collected validation errors.</param>
    /// <param name="nameToOrdinal">
    /// The schema's mapping from property names to ordinal indices.
    /// Enables the string indexer (<c>result["name"]</c>).
    /// </param>
    /// <param name="arrayBuffer">
    /// Optional array buffer for array element storage. Disposed along with other resources.
    /// </param>
    internal ParseResult(OffsetTable offsetTable, ErrorCollector errorCollector, Dictionary<string, int> nameToOrdinal, ArrayBuffer? arrayBuffer)
    {
        _offsetTable = offsetTable;
        _errorCollector = errorCollector;
        _nameToOrdinal = nameToOrdinal;
        _arrayBuffer = arrayBuffer;
    }

    /// <summary>
    /// Whether the parse result is valid (no validation errors were collected).
    /// Returns <c>true</c> for a default (uninitialized) instance.
    /// </summary>
    public bool IsValid => !_errorCollector.HasErrors;

    /// <summary>
    /// The collected validation errors. Empty when <see cref="IsValid"/> is <c>true</c>.
    /// Always accessible regardless of validity (uniform API).
    /// </summary>
    public ErrorCollector Errors => _errorCollector;

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> at the given ordinal index.
    /// Returns <see cref="ParsedProperty.Empty"/> if the ordinal is out of range or the slot was never set.
    /// </summary>
    /// <param name="ordinal">The schema-determined ordinal for the property.</param>
    public ParsedProperty this[int ordinal] => _offsetTable[ordinal];

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> with the given name.
    /// Returns <see cref="ParsedProperty.Empty"/> if the name is not in the schema or the property was absent.
    /// </summary>
    /// <param name="name">The property name to look up.</param>
    public ParsedProperty this[string name]
    {
        get
        {
            if (_nameToOrdinal is not null)
            {
                if (_nameToOrdinal.TryGetValue(name, out int ordinal))
                    return _offsetTable[ordinal];
                if (name.Length > 0 && name[0] != '/' && _nameToOrdinal.TryGetValue("/" + name, out ordinal))
                    return _offsetTable[ordinal];
            }

            return ParsedProperty.Empty;
        }
    }

    /// <summary>
    /// Returns a struct enumerator over all parsed properties that have values,
    /// enabling <c>foreach</c> support without allocation.
    /// </summary>
    public Enumerator GetEnumerator() => new Enumerator(_offsetTable);

    /// <summary>
    /// Cascades disposal to the underlying <see cref="OffsetTable"/> and <see cref="ErrorCollector"/>,
    /// returning their ArrayPool buffers. Safe to call on a default instance.
    /// </summary>
    public void Dispose()
    {
        _offsetTable.Dispose();
        _errorCollector.Dispose();
        _arrayBuffer?.Dispose();
    }

    /// <summary>
    /// A stack-allocated enumerator over <see cref="ParsedProperty"/> entries that have values.
    /// Skips empty (unset) slots in the offset table.
    /// </summary>
    public struct Enumerator
    {
        private readonly OffsetTable _table;
        private readonly int _count;
        private int _index;

        internal Enumerator(OffsetTable table)
        {
            _table = table;
            _count = table.Count;
            _index = -1;
        }

        /// <summary>Gets the current <see cref="ParsedProperty"/>.</summary>
        public ParsedProperty Current => _table[_index];

        /// <summary>Advances the enumerator to the next property that has a value.</summary>
        public bool MoveNext()
        {
            while (true)
            {
                _index++;
                if (_index >= _count)
                    return false;

                if (_table[_index].HasValue)
                    return true;
            }
        }
    }
}
