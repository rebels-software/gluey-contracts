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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gluey.Contract.Binary.Dto;

/// <summary>
/// DTO for a single field in a binary contract.
/// The <c>fields</c> property is polymorphic: for "bits" containers it holds <see cref="BitFieldDto"/> objects,
/// for "struct" element types it holds nested <see cref="FieldDto"/> objects. Stored as <see cref="JsonElement"/>
/// and deserialized on demand by <see cref="Schema.BinaryContractLoader"/>.
/// </summary>
internal sealed class FieldDto
{
    [JsonPropertyName("dependsOn")]
    public string? DependsOn { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    [JsonPropertyName("validation")]
    public ValidationDto? Validation { get; set; }

    [JsonPropertyName("displayName")]
    public Dictionary<string, string>? DisplayName { get; set; }

    /// <summary>
    /// Polymorphic sub-fields: bit-field definitions for "bits" containers,
    /// or full field definitions for "struct" elements. Stored as raw JSON.
    /// </summary>
    [JsonPropertyName("fields")]
    public JsonElement? Fields { get; set; }

    /// <summary>Polymorphic: int for fixed arrays, string for semi-dynamic arrays.</summary>
    [JsonPropertyName("count")]
    public JsonElement? Count { get; set; }

    /// <summary>Array element type definition.</summary>
    [JsonPropertyName("element")]
    public FieldDto? Element { get; set; }

    /// <summary>Enum base primitive type (e.g. "uint8").</summary>
    [JsonPropertyName("primitive")]
    public string? Primitive { get; set; }

    /// <summary>Enum value mappings (integer key as string to label).</summary>
    [JsonPropertyName("values")]
    public Dictionary<string, string>? Values { get; set; }

    /// <summary>Custom error metadata from x-error extension.</summary>
    [JsonPropertyName("x-error")]
    public JsonElement? XError { get; set; }

    /// <summary>Custom description from x-description extension.</summary>
    [JsonPropertyName("x-description")]
    public string? XDescription { get; set; }
}
