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

namespace Gluey.Contract.Json;

/// <summary>
/// Represents the JSON Schema "type" keyword values as a flags enum.
/// Multiple types can be combined (e.g., <c>SchemaType.String | SchemaType.Null</c>
/// for <c>"type": ["string", "null"]</c>).
/// </summary>
[Flags]
internal enum SchemaType : byte
{
    None    = 0,
    Null    = 1 << 0,   // 1
    Boolean = 1 << 1,   // 2
    Integer = 1 << 2,   // 4
    Number  = 1 << 3,   // 8
    String  = 1 << 4,   // 16
    Array   = 1 << 5,   // 32
    Object  = 1 << 6,   // 64
}
