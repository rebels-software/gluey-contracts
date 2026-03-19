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

using Microsoft.AspNetCore.Http;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Configuration options for Gluey.Contract ASP.NET Core integration.
/// </summary>
public class ContractOptions
{
    /// <summary>
    /// Optional per-error transformer. Called for each <see cref="ValidationError"/> before building
    /// the response. Return a transformed object to override the error, or <c>null</c> to use the default.
    /// </summary>
    public Func<ValidationError, HttpContext, object?>? TransformError { get; set; }

    /// <summary>
    /// Optional full response override. When set, replaces the default RFC 7807 Problem Details response.
    /// Called with the <see cref="ErrorCollector"/> and <see cref="HttpContext"/> when validation fails.
    /// </summary>
    public Func<ErrorCollector, HttpContext, Task>? OnValidationFailed { get; set; }
}
