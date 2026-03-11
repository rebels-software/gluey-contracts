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
/// URI-keyed registry of loaded schema root nodes.
/// Used by reference resolution to locate cross-schema <c>$ref</c> targets.
/// </summary>
/// <remarks>
/// The class is public so it can be passed as a parameter to public API methods
/// (e.g., <c>JsonContractSchema.TryLoad</c>), but its mutation methods are internal
/// because they accept/return the internal <see cref="SchemaNode"/> type.
/// </remarks>
public sealed class SchemaRegistry
{
    private readonly Dictionary<string, SchemaNode> _schemas = new(StringComparer.Ordinal);

    /// <summary>Gets the number of schemas registered.</summary>
    public int Count => _schemas.Count;

    /// <summary>
    /// Registers a schema root node under the given URI.
    /// If a schema is already registered under the same URI, it is overwritten.
    /// </summary>
    /// <param name="uri">The schema URI (trailing slashes are trimmed).</param>
    /// <param name="root">The root <see cref="SchemaNode"/> of the schema.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> or <paramref name="root"/> is <c>null</c>.</exception>
    internal void Add(string uri, SchemaNode root)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(root);

        _schemas[NormalizeUri(uri)] = root;
    }

    /// <summary>
    /// Attempts to retrieve a previously registered schema root node by URI.
    /// </summary>
    /// <param name="uri">The schema URI to look up (trailing slashes are trimmed).</param>
    /// <param name="root">When this method returns <c>true</c>, the root node; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a schema was found; otherwise <c>false</c>.</returns>
    internal bool TryGet(string uri, out SchemaNode? root)
    {
        return _schemas.TryGetValue(NormalizeUri(uri), out root);
    }

    private static string NormalizeUri(string uri)
    {
        return uri.TrimEnd('/');
    }
}
