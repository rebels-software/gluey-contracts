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

using Microsoft.Extensions.DependencyInjection;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Extension methods for registering Gluey.Contract services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Gluey.Contract services to the service collection.
    /// Registers the <see cref="ContractSchemaRegistry"/> and <see cref="ContractOptions"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for <see cref="ContractOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGlueyContracts(this IServiceCollection services, Action<ContractOptions>? configure = null)
    {
        var options = new ContractOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ContractSchemaRegistry>();

        return services;
    }

    /// <summary>
    /// Adds Gluey.Contract services and registers schemas via the <see cref="ContractSchemaRegistry"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureSchemas">Callback to register schemas.</param>
    /// <param name="configureOptions">Optional configuration callback for <see cref="ContractOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGlueyContracts(
        this IServiceCollection services,
        Action<ContractSchemaRegistry> configureSchemas,
        Action<ContractOptions>? configureOptions = null)
    {
        var options = new ContractOptions();
        configureOptions?.Invoke(options);

        var registry = new ContractSchemaRegistry();
        configureSchemas(registry);

        services.AddSingleton(options);
        services.AddSingleton(registry);

        return services;
    }
}
