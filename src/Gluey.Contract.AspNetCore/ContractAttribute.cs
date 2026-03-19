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

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Marks an endpoint with a JSON Schema contract for request validation.
/// The schema is loaded at startup and cached for the lifetime of the application.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ContractAttribute : Attribute
{
    /// <summary>
    /// The schema identifier used to resolve the <see cref="IContractSchema"/>
    /// from the <see cref="ContractSchemaRegistry"/>.
    /// </summary>
    public string SchemaName { get; }

    /// <summary>
    /// Creates a new <see cref="ContractAttribute"/> with the given schema name.
    /// </summary>
    /// <param name="schemaName">The schema identifier (e.g. "schemas/create-order.json").</param>
    public ContractAttribute(string schemaName)
    {
        SchemaName = schemaName;
    }
}
