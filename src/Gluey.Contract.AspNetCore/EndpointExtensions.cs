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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Extension methods for adding contract validation to endpoints.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Adds contract validation to this endpoint using the specified schema.
    /// Validates the request body before the handler executes and short-circuits
    /// with a 400 response on failure.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="schema">The compiled schema to validate against.</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder WithContractValidation<TBuilder>(this TBuilder builder, IContractSchema schema)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new ContractValidationFilter(schema));
        return builder;
    }

    /// <summary>
    /// Adds contract validation to this endpoint using a named schema from the registry.
    /// The schema is resolved from the <see cref="ContractSchemaRegistry"/> at request time.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="schemaName">The schema name registered in <see cref="ContractSchemaRegistry"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder WithContractValidation<TBuilder>(this TBuilder builder, string schemaName)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new NamedContractValidationFilter(schemaName));
        return builder;
    }

    /// <summary>
    /// Adds the validation filter for endpoints using <see cref="ContractBody"/> parameter binding
    /// with <c>[Contract]</c> attribute. Call this once after mapping endpoints.
    /// Not needed when using <see cref="WithContractValidation{TBuilder}(TBuilder, IContractSchema)"/>.
    /// </summary>
    public static TBuilder WithContract<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new ContractBodyValidationFilter());
        return builder;
    }

    /// <summary>
    /// Adds a per-endpoint error transformer that overrides the global <see cref="ContractOptions.TransformError"/>.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="transform">The per-endpoint error transformer.</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder WithContractErrors<TBuilder>(
        this TBuilder builder,
        Func<ValidationError, HttpContext, object?> transform)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(new ContractErrorTransformMetadata(transform));
        });
        return builder;
    }
}

/// <summary>
/// Metadata for per-endpoint error transformation.
/// </summary>
internal sealed class ContractErrorTransformMetadata
{
    internal Func<ValidationError, HttpContext, object?> Transform { get; }

    internal ContractErrorTransformMetadata(Func<ValidationError, HttpContext, object?> transform)
    {
        Transform = transform;
    }
}

/// <summary>
/// Endpoint filter that resolves a schema by name from the registry at request time.
/// </summary>
internal sealed class NamedContractValidationFilter : IEndpointFilter
{
    private readonly string _schemaName;
    private IContractSchema? _cached;

    internal NamedContractValidationFilter(string schemaName)
    {
        _schemaName = schemaName;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (_cached is null)
        {
            var registry = context.HttpContext.RequestServices.GetService(typeof(ContractSchemaRegistry)) as ContractSchemaRegistry
                ?? throw new InvalidOperationException(
                    $"ContractSchemaRegistry not found. Call builder.Services.AddGlueyContracts() in your startup.");
            _cached = registry.Get(_schemaName);
        }

        var filter = new ContractValidationFilter(_cached);
        return filter.InvokeAsync(context, next);
    }
}
