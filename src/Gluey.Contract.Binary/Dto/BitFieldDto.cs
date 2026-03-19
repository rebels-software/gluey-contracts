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
/// DTO for a bit-level sub-field within a "bits" container.
/// </summary>
internal sealed class BitFieldDto
{
    [JsonPropertyName("bit")]
    public int Bit { get; set; }

    [JsonPropertyName("bits")]
    public int Bits { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
