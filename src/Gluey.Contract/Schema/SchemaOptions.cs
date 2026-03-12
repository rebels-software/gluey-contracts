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
/// Configuration options that control schema validation behavior.
/// </summary>
public sealed class SchemaOptions
{
    /// <summary>
    /// When <c>true</c>, the <c>format</c> keyword produces validation errors
    /// for values that do not match the declared format.
    /// When <c>false</c> (default), <c>format</c> is treated as an annotation only.
    /// </summary>
    /// <remarks>
    /// Format assertion may allocate (string conversions for .NET parser APIs).
    /// This is a documented exception to the zero-allocation guarantee.
    /// </remarks>
    public bool AssertFormat { get; init; } = false;
}
