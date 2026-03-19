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
/// Maps <see cref="ErrorCollector"/> to RFC 7807 Problem Details response.
/// </summary>
internal static class ProblemDetailsMapper
{
    /// <summary>
    /// Builds an RFC 7807 Problem Details object from validation errors.
    /// When <see cref="ContractOptions.TransformError"/> is set, each error
    /// is passed through the transformer before inclusion.
    /// </summary>
    internal static ContractProblemDetails Build(ErrorCollector errors, HttpContext context, ContractOptions? options)
    {
        var errorList = new List<object>(errors.Count);

        for (int i = 0; i < errors.Count; i++)
        {
            var error = errors[i];

            if (options?.TransformError is { } transform)
            {
                var transformed = transform(error, context);
                if (transformed is not null)
                {
                    errorList.Add(transformed);
                    continue;
                }
            }

            errorList.Add(MapError(error));
        }

        return new ContractProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Errors = errorList
        };
    }

    private static ContractValidationError MapError(ValidationError error)
    {
        var mapped = new ContractValidationError
        {
            Path = error.Path,
            Code = error.ErrorInfo?.Code ?? error.Code.ToString(),
            Message = error.Message
        };

        if (error.ErrorInfo is { } info)
        {
            if (info.Title is not null) mapped.Title = info.Title;
            if (info.Type is not null) mapped.Type = info.Type;
        }

        return mapped;
    }
}

/// <summary>
/// RFC 7807 Problem Details response with contract validation errors.
/// </summary>
public class ContractProblemDetails
{
    /// <summary>URI reference identifying the problem type.</summary>
    public string? Type { get; set; }

    /// <summary>Short, human-readable summary.</summary>
    public string? Title { get; set; }

    /// <summary>HTTP status code.</summary>
    public int? Status { get; set; }

    /// <summary>The validation errors.</summary>
    public List<object>? Errors { get; set; }
}

/// <summary>
/// A single validation error in the Problem Details response.
/// </summary>
public class ContractValidationError
{
    /// <summary>RFC 6901 JSON Pointer path to the failing property.</summary>
    public string? Path { get; set; }

    /// <summary>Machine-readable error code.</summary>
    public string? Code { get; set; }

    /// <summary>Human-readable error message.</summary>
    public string? Message { get; set; }

    /// <summary>Short error title (from x-error).</summary>
    public string? Title { get; set; }

    /// <summary>Error type URI (from x-error).</summary>
    public string? Type { get; set; }
}
