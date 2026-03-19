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
using Microsoft.Extensions.DependencyInjection;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Endpoint filter that validates the request body against an <see cref="IContractSchema"/>
/// before the handler executes. Short-circuits with a 400 response on validation failure.
/// </summary>
internal sealed class ContractValidationFilter : IEndpointFilter
{
    private readonly IContractSchema _schema;

    internal ContractValidationFilter(IContractSchema schema)
    {
        _schema = schema;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Read the request body directly into a byte array
        var body = await ReadBodyAsync(httpContext.Request);

        // Parse and validate — returns null for empty/structurally invalid input
        using var result = _schema.Parse(body);

        if (result is null)
        {
            return Results.Json(new ContractProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Errors = [new ContractValidationError
                {
                    Path = "",
                    Code = "InvalidData",
                    Message = "Request body is empty or structurally invalid."
                }]
            }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!result.Value.IsValid)
        {
            var options = httpContext.RequestServices.GetService<ContractOptions>();

            if (options?.OnValidationFailed is { } handler)
            {
                await handler(result.Value.Errors, httpContext);
                return Results.Empty;
            }

            var problemDetails = ProblemDetailsMapper.Build(result.Value.Errors, httpContext, options);
            return Results.Json(problemDetails, statusCode: StatusCodes.Status400BadRequest);
        }

        // Store the validated body and schema for GetContractResult() / ContractBody binding
        httpContext.Items["Contract:Body"] = body;
        httpContext.Items["Contract:Schema"] = _schema;

        return await next(context);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is > 0)
        {
            var body = new byte[(int)request.ContentLength.Value];
            var totalRead = 0;
            while (totalRead < body.Length)
            {
                var read = await request.Body.ReadAsync(body.AsMemory(totalRead));
                if (read == 0) break;
                totalRead += read;
            }
            return body;
        }

        // Chunked or unknown length — read to end
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}
