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

namespace Gluey.Contract;

/// <summary>
/// Format-agnostic contract schema. Validates raw bytes and produces a <see cref="ParseResult"/>.
/// Implemented by format-specific schema types (e.g. <c>JsonContractSchema</c>, <c>ProtobufContractSchema</c>).
/// </summary>
public interface IContractSchema
{
    /// <summary>
    /// Parses and validates the given byte array against this schema.
    /// Returns <c>null</c> if the input is structurally invalid.
    /// </summary>
    /// <param name="data">The raw byte array to parse.</param>
    /// <returns>A <see cref="ParseResult"/> or <c>null</c>.</returns>
    ParseResult? Parse(byte[] data);

    /// <summary>
    /// Parses and validates the given byte span against this schema.
    /// Validates only (no property access via offset table).
    /// Returns <c>null</c> if the input is structurally invalid.
    /// </summary>
    /// <param name="data">The raw bytes to parse.</param>
    /// <returns>A <see cref="ParseResult"/> or <c>null</c>.</returns>
    ParseResult? Parse(ReadOnlySpan<byte> data);
}
