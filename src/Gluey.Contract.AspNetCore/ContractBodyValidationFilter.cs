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
/// Endpoint filter that checks if <see cref="ContractBody"/> binding detected a validation failure
/// and short-circuits with the appropriate error response.
/// Automatically added to endpoints that use <c>[Contract]</c> attribute with <see cref="ContractBody"/> parameter.
/// </summary>
internal sealed class ContractBodyValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Find the ContractBody argument
        for (int i = 0; i < context.Arguments.Count; i++)
        {
            if (context.Arguments[i] is ContractBody body && body.ValidationFailure is { } failure)
            {
                if (failure is DeferredValidationFailure deferred)
                {
                    await deferred.Handler(deferred.Errors, context.HttpContext);
                    return Results.Empty;
                }

                return Results.Json(failure, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }
}
