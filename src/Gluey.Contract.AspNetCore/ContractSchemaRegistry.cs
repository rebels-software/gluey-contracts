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

using System.Collections.Concurrent;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Thread-safe registry of named <see cref="IContractSchema"/> instances.
/// Schemas are loaded once and cached for the application lifetime.
/// Works with any format — JSON Schema, Protobuf, etc.
/// </summary>
public sealed class ContractSchemaRegistry
{
    private readonly ConcurrentDictionary<string, IContractSchema> _schemas = new();

    /// <summary>
    /// Registers a schema with the given name.
    /// </summary>
    /// <param name="name">The schema identifier.</param>
    /// <param name="schema">The compiled schema.</param>
    public void Add(string name, IContractSchema schema)
    {
        _schemas[name] = schema;
    }

    /// <summary>
    /// Tries to retrieve a schema by name.
    /// </summary>
    /// <param name="name">The schema identifier.</param>
    /// <param name="schema">The resolved schema, or <c>null</c>.</param>
    /// <returns><c>true</c> if found.</returns>
    public bool TryGet(string name, out IContractSchema? schema)
    {
        return _schemas.TryGetValue(name, out schema);
    }

    /// <summary>
    /// Retrieves a schema by name. Throws <see cref="KeyNotFoundException"/> if not found.
    /// </summary>
    public IContractSchema Get(string name)
    {
        if (_schemas.TryGetValue(name, out var schema))
            return schema;
        throw new KeyNotFoundException($"Schema '{name}' not found in the contract registry.");
    }
}
