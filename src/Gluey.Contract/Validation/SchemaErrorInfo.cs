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
/// Custom error metadata from the <c>x-error</c> JSON Schema extension.
/// Fields map to RFC 7807 Problem Details: code, title, detail, type.
/// All fields are optional — omitted fields fall through to library defaults.
/// </summary>
public readonly struct SchemaErrorInfo
{
    /// <summary>Machine-readable error code (e.g. "ORDER_QUANTITY_EXCEEDED").</summary>
    public readonly string? Code;

    /// <summary>Short human-readable summary (e.g. "Quantity limit exceeded").</summary>
    public readonly string? Title;

    /// <summary>Detailed human-readable explanation (e.g. "Maximum 6 items per order").</summary>
    public readonly string? Detail;

    /// <summary>URI identifying the error type (e.g. "https://api.example.com/errors/quantity-exceeded").</summary>
    public readonly string? Type;

    /// <summary>
    /// Creates a new <see cref="SchemaErrorInfo"/>.
    /// </summary>
    public SchemaErrorInfo(string? code, string? title, string? detail, string? type)
    {
        Code = code;
        Title = title;
        Detail = detail;
        Type = type;
    }
}
