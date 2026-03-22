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

using System.Text.Json.Serialization;

namespace Gluey.Contract.Binary.Dto;

/// <summary>
/// Top-level contract DTO for System.Text.Json deserialization of binary contract JSON.
/// </summary>
internal sealed class ContractDto
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("displayName")]
    public Dictionary<string, string>? DisplayName { get; set; }

    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDto>? Fields { get; set; }
}
