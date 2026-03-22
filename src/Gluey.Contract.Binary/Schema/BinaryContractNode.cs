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

using System.Text.RegularExpressions;

namespace Gluey.Contract.Binary.Schema;

// -- Supporting records --

/// <summary>Describes a single bit-level sub-field within a bits container.</summary>
internal sealed record BitFieldInfo(int Bit, int Bits, string Type);

/// <summary>Describes the element type of an array field.</summary>
internal sealed record ArrayElementInfo(string Type, int Size, BinaryContractNode[]? StructFields);

/// <summary>Per-field validation rules from the contract JSON.</summary>
internal sealed record ValidationRules(double? Min, double? Max, string? Pattern, int? MinLength, int? MaxLength);

/// <summary>Contract-level metadata (id, name, version, displayName).</summary>
internal sealed record ContractMetadata(string? Id, string? Name, string? Version, Dictionary<string, string>? DisplayName);

// -- Main node class --

/// <summary>
/// Internal model representing a single field in a binary contract.
/// Mirrors the SchemaNode pattern: single sealed class with nullable fields per type.
/// Allocated once at contract load time; not on the parse/validation hot path.
/// </summary>
internal sealed class BinaryContractNode
{
    // -- Identity --

    /// <summary>Field name from the contract JSON.</summary>
    internal string Name { get; init; } = "";

    /// <summary>Name of the field this one depends on (null for root).</summary>
    internal string? DependsOn { get; init; }

    // -- Type info --

    /// <summary>Field type (e.g. "uint16", "bits", "array", "enum", "string", "struct", "padding").</summary>
    internal string Type { get; init; } = "";

    /// <summary>Declared size in bytes.</summary>
    internal int Size { get; init; }

    /// <summary>String encoding (e.g. "ASCII", "UTF-8"). Null for non-string types.</summary>
    internal string? Encoding { get; init; }

    /// <summary>String trim mode: 0=plain, 1=trimStart, 2=trimEnd (default), 3=trim. Only for string fields.</summary>
    internal byte StringMode { get; init; }

    // -- Resolved at load time (set by BinaryChainResolver in Plan 02) --

    /// <summary>Absolute byte offset in the payload, computed from the dependency chain.</summary>
    internal int AbsoluteOffset { get; set; }

    /// <summary>Resolved endianness: 0 = little, 1 = big.</summary>
    internal byte ResolvedEndianness { get; set; }

    /// <summary>Whether this field's offset depends on a semi-dynamic array earlier in the chain.</summary>
    internal bool IsDynamicOffset { get; set; }

    // -- Per-field endianness from contract JSON (null = use contract default) --

    /// <summary>Per-field endianness override from contract JSON.</summary>
    internal string? Endianness { get; init; }

    // -- Type-specific (nullable per SchemaNode pattern) --

    /// <summary>Bit sub-fields for a "bits" container. Keyed by sub-field name.</summary>
    internal Dictionary<string, BitFieldInfo>? BitFields { get; init; }

    /// <summary>Array element descriptor.</summary>
    internal ArrayElementInfo? ArrayElement { get; init; }

    /// <summary>Enum value mappings (integer key as string to label).</summary>
    internal Dictionary<string, string>? EnumValues { get; init; }

    /// <summary>Enum base primitive type (e.g. "uint8").</summary>
    internal string? EnumPrimitive { get; init; }

    /// <summary>Array count: int for fixed, string for semi-dynamic (references another field).</summary>
    internal object? Count { get; init; }

    // -- Validation rules --

    /// <summary>Per-field validation rules (min, max, pattern, etc.).</summary>
    internal ValidationRules? Validation { get; init; }

    /// <summary>Pre-compiled regex for pattern validation. Compiled at load time for performance.</summary>
    internal Regex? CompiledPattern { get; init; }

    // -- Extensions --

    /// <summary>Custom error metadata from x-error extension.</summary>
    internal SchemaErrorInfo? ErrorInfo { get; init; }

    /// <summary>Custom description from x-description extension.</summary>
    internal string? XDescription { get; init; }

    // -- Display --

    /// <summary>Localized display names.</summary>
    internal Dictionary<string, string>? DisplayName { get; init; }

    // -- Struct sub-fields (for array elements with type "struct") --

    /// <summary>Sub-fields for struct-typed array elements.</summary>
    internal BinaryContractNode[]? StructFields { get; init; }
}
