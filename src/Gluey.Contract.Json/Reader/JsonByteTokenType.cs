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

namespace Gluey.Contract.Json;

/// <summary>
/// Identifies the type of a JSON token produced by <see cref="JsonByteReader"/>.
/// Decoupled from <see cref="System.Text.Json.JsonTokenType"/> to avoid BCL coupling.
/// </summary>
internal enum JsonByteTokenType : byte
{
    None = 0,
    StartObject,
    EndObject,
    StartArray,
    EndArray,
    PropertyName,
    String,
    Number,
    True,
    False,
    Null,
}
