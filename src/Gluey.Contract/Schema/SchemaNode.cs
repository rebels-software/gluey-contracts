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

using System.Text.RegularExpressions;

namespace Gluey.Contract;

/// <summary>
/// Immutable tree node representing a compiled JSON Schema (Draft 2020-12).
/// Each node carries all keyword fields and a precomputed RFC 6901 JSON Pointer path.
/// Allocated once at schema load time; not on the parse/validation hot path.
/// </summary>
internal sealed class SchemaNode
{
    // ── Static sentinels for boolean schemas ─────────────────────────────

    /// <summary>Boolean schema <c>true</c> — accepts any instance.</summary>
    internal static readonly SchemaNode True = new("", booleanSchema: true);

    /// <summary>Boolean schema <c>false</c> — rejects any instance.</summary>
    internal static readonly SchemaNode False = new("", booleanSchema: false);

    // ── Identity / Core ──────────────────────────────────────────────────

    /// <summary>RFC 6901 JSON Pointer path, precomputed at load time. Root is <c>""</c>.</summary>
    internal string Path { get; }

    /// <summary>The <c>$id</c> keyword.</summary>
    internal string? Id { get; }

    /// <summary>The <c>$anchor</c> keyword.</summary>
    internal string? Anchor { get; }

    /// <summary>The <c>$ref</c> keyword (stored as string, resolved in Phase 3).</summary>
    internal string? Ref { get; }

    /// <summary>The resolved target node for <see cref="Ref"/>. Set by SchemaRefResolver after tree construction.</summary>
    internal SchemaNode? ResolvedRef { get; set; }

    /// <summary>The <c>$dynamicRef</c> keyword.</summary>
    internal string? DynamicRef { get; }

    /// <summary>The <c>$dynamicAnchor</c> keyword.</summary>
    internal string? DynamicAnchor { get; }

    /// <summary>The <c>$defs</c> keyword — named schema definitions.</summary>
    internal Dictionary<string, SchemaNode>? Defs { get; }

    /// <summary>The <c>$comment</c> keyword.</summary>
    internal string? Comment { get; }

    // ── Type ─────────────────────────────────────────────────────────────

    /// <summary>The <c>type</c> keyword.</summary>
    internal SchemaType? Type { get; }

    // ── Validation — Any instance ────────────────────────────────────────

    /// <summary>The <c>enum</c> keyword — each value stored as raw UTF-8 bytes.</summary>
    internal byte[][]? Enum { get; }

    /// <summary>The <c>const</c> keyword — value stored as raw UTF-8 bytes.</summary>
    internal byte[]? Const { get; }

    // ── Validation — Numeric ─────────────────────────────────────────────

    /// <summary>The <c>minimum</c> keyword.</summary>
    internal decimal? Minimum { get; }

    /// <summary>The <c>maximum</c> keyword.</summary>
    internal decimal? Maximum { get; }

    /// <summary>The <c>exclusiveMinimum</c> keyword.</summary>
    internal decimal? ExclusiveMinimum { get; }

    /// <summary>The <c>exclusiveMaximum</c> keyword.</summary>
    internal decimal? ExclusiveMaximum { get; }

    /// <summary>The <c>multipleOf</c> keyword.</summary>
    internal decimal? MultipleOf { get; }

    // ── Validation — String ──────────────────────────────────────────────

    /// <summary>The <c>minLength</c> keyword.</summary>
    internal int? MinLength { get; }

    /// <summary>The <c>maxLength</c> keyword.</summary>
    internal int? MaxLength { get; }

    /// <summary>The <c>pattern</c> keyword.</summary>
    internal string? Pattern { get; }

    /// <summary>Pre-compiled <see cref="Regex"/> for the <c>pattern</c> keyword. Compiled at schema load time.</summary>
    internal Regex? CompiledPattern { get; }

    // ── Validation — Array ───────────────────────────────────────────────

    /// <summary>The <c>minItems</c> keyword.</summary>
    internal int? MinItems { get; }

    /// <summary>The <c>maxItems</c> keyword.</summary>
    internal int? MaxItems { get; }

    /// <summary>The <c>minContains</c> keyword.</summary>
    internal int? MinContains { get; }

    /// <summary>The <c>maxContains</c> keyword.</summary>
    internal int? MaxContains { get; }

    /// <summary>The <c>uniqueItems</c> keyword.</summary>
    internal bool? UniqueItems { get; }

    // ── Validation — Object ──────────────────────────────────────────────

    /// <summary>The <c>required</c> keyword.</summary>
    internal string[]? Required { get; }

    /// <summary>The <c>minProperties</c> keyword.</summary>
    internal int? MinProperties { get; }

    /// <summary>The <c>maxProperties</c> keyword.</summary>
    internal int? MaxProperties { get; }

    /// <summary>The <c>dependentRequired</c> keyword.</summary>
    internal Dictionary<string, string[]>? DependentRequired { get; }

    // ── Applicator — Object ──────────────────────────────────────────────

    /// <summary>The <c>properties</c> keyword.</summary>
    internal Dictionary<string, SchemaNode>? Properties { get; }

    /// <summary>The <c>additionalProperties</c> keyword (schema or boolean schema).</summary>
    internal SchemaNode? AdditionalProperties { get; }

    /// <summary>The <c>patternProperties</c> keyword.</summary>
    internal Dictionary<string, SchemaNode>? PatternProperties { get; }

    /// <summary>The <c>propertyNames</c> keyword.</summary>
    internal SchemaNode? PropertyNames { get; }

    /// <summary>Pre-compiled regex patterns for the <c>patternProperties</c> keyword. Compiled at schema load time.</summary>
    internal (Regex Pattern, SchemaNode Schema)[]? CompiledPatternProperties { get; }

    /// <summary>The <c>dependentSchemas</c> keyword.</summary>
    internal Dictionary<string, SchemaNode>? DependentSchemas { get; }

    // ── Applicator — Array ───────────────────────────────────────────────

    /// <summary>The <c>items</c> keyword.</summary>
    internal SchemaNode? Items { get; }

    /// <summary>The <c>prefixItems</c> keyword.</summary>
    internal SchemaNode[]? PrefixItems { get; }

    /// <summary>The <c>contains</c> keyword.</summary>
    internal SchemaNode? Contains { get; }

    // ── Applicator — Composition ─────────────────────────────────────────

    /// <summary>The <c>allOf</c> keyword.</summary>
    internal SchemaNode[]? AllOf { get; }

    /// <summary>The <c>anyOf</c> keyword.</summary>
    internal SchemaNode[]? AnyOf { get; }

    /// <summary>The <c>oneOf</c> keyword.</summary>
    internal SchemaNode[]? OneOf { get; }

    /// <summary>The <c>not</c> keyword.</summary>
    internal SchemaNode? Not { get; }

    // ── Applicator — Conditional ─────────────────────────────────────────

    /// <summary>The <c>if</c> keyword.</summary>
    internal SchemaNode? If { get; }

    /// <summary>The <c>then</c> keyword.</summary>
    internal SchemaNode? Then { get; }

    /// <summary>The <c>else</c> keyword.</summary>
    internal SchemaNode? Else { get; }

    // ── Meta-data (annotations) ──────────────────────────────────────────

    /// <summary>The <c>title</c> keyword.</summary>
    internal string? Title { get; }

    /// <summary>The <c>description</c> keyword.</summary>
    internal string? Description { get; }

    /// <summary>The <c>format</c> keyword.</summary>
    internal string? Format { get; }

    /// <summary>The <c>deprecated</c> keyword.</summary>
    internal bool? Deprecated { get; }

    // ── Boolean schema sentinel ──────────────────────────────────────────

    /// <summary>
    /// Non-null means this node is a boolean schema.
    /// <c>true</c> = accept all instances, <c>false</c> = reject all instances.
    /// </summary>
    internal bool? BooleanSchema { get; }

    // ── Constructor ──────────────────────────────────────────────────────

    internal SchemaNode(
        string path,
        // Identity / Core
        string? id = null,
        string? anchor = null,
        string? @ref = null,
        string? dynamicRef = null,
        string? dynamicAnchor = null,
        Dictionary<string, SchemaNode>? defs = null,
        string? comment = null,
        // Type
        SchemaType? type = null,
        // Validation — Any instance
        byte[][]? @enum = null,
        byte[]? @const = null,
        // Validation — Numeric
        decimal? minimum = null,
        decimal? maximum = null,
        decimal? exclusiveMinimum = null,
        decimal? exclusiveMaximum = null,
        decimal? multipleOf = null,
        // Validation — String
        int? minLength = null,
        int? maxLength = null,
        string? pattern = null,
        Regex? compiledPattern = null,
        // Validation — Array
        int? minItems = null,
        int? maxItems = null,
        int? minContains = null,
        int? maxContains = null,
        bool? uniqueItems = null,
        // Validation — Object
        string[]? required = null,
        int? minProperties = null,
        int? maxProperties = null,
        Dictionary<string, string[]>? dependentRequired = null,
        // Applicator — Object
        Dictionary<string, SchemaNode>? properties = null,
        SchemaNode? additionalProperties = null,
        Dictionary<string, SchemaNode>? patternProperties = null,
        SchemaNode? propertyNames = null,
        Dictionary<string, SchemaNode>? dependentSchemas = null,
        (Regex Pattern, SchemaNode Schema)[]? compiledPatternProperties = null,
        // Applicator — Array
        SchemaNode? items = null,
        SchemaNode[]? prefixItems = null,
        SchemaNode? contains = null,
        // Applicator — Composition
        SchemaNode[]? allOf = null,
        SchemaNode[]? anyOf = null,
        SchemaNode[]? oneOf = null,
        SchemaNode? not = null,
        // Applicator — Conditional
        SchemaNode? @if = null,
        SchemaNode? then = null,
        SchemaNode? @else = null,
        // Meta-data
        string? title = null,
        string? description = null,
        string? format = null,
        bool? deprecated = null,
        // Boolean schema
        bool? booleanSchema = null)
    {
        Path = path;
        Id = id;
        Anchor = anchor;
        Ref = @ref;
        DynamicRef = dynamicRef;
        DynamicAnchor = dynamicAnchor;
        Defs = defs;
        Comment = comment;
        Type = type;
        Enum = @enum;
        Const = @const;
        Minimum = minimum;
        Maximum = maximum;
        ExclusiveMinimum = exclusiveMinimum;
        ExclusiveMaximum = exclusiveMaximum;
        MultipleOf = multipleOf;
        MinLength = minLength;
        MaxLength = maxLength;
        Pattern = pattern;
        CompiledPattern = compiledPattern;
        MinItems = minItems;
        MaxItems = maxItems;
        MinContains = minContains;
        MaxContains = maxContains;
        UniqueItems = uniqueItems;
        Required = required;
        MinProperties = minProperties;
        MaxProperties = maxProperties;
        DependentRequired = dependentRequired;
        Properties = properties;
        AdditionalProperties = additionalProperties;
        PatternProperties = patternProperties;
        PropertyNames = propertyNames;
        DependentSchemas = dependentSchemas;
        CompiledPatternProperties = compiledPatternProperties;
        Items = items;
        PrefixItems = prefixItems;
        Contains = contains;
        AllOf = allOf;
        AnyOf = anyOf;
        OneOf = oneOf;
        Not = not;
        If = @if;
        Then = then;
        Else = @else;
        Title = title;
        Description = description;
        Format = format;
        Deprecated = deprecated;
        BooleanSchema = booleanSchema;
    }

    // ── Pre-compiled property lookup (populated by SchemaIndexer) ────────

    /// <summary>
    /// Pre-compiled lookup entry for fast UTF8-based property resolution.
    /// Avoids string allocation during parse by matching raw UTF8 bytes.
    /// </summary>
    internal sealed class PropertyEntry
    {
        internal readonly byte[] Utf8Name;
        internal readonly string Name;
        internal readonly SchemaNode Child;
        internal readonly string ChildPath;
        internal int Ordinal;

        /// <summary>
        /// Pre-computed child ordinals for hierarchical property access.
        /// Maps child property names to their ordinals. Null if this property has no sub-properties.
        /// </summary>
        internal Dictionary<string, int>? GrandchildOrdinals;

        /// <summary>
        /// Index in the parent node's Required array, or -1 if not required.
        /// Used for bitset-based required tracking.
        /// </summary>
        internal int RequiredIndex;

        internal PropertyEntry(byte[] utf8Name, string name, SchemaNode child, string childPath)
        {
            Utf8Name = utf8Name;
            Name = name;
            Child = child;
            ChildPath = childPath;
            Ordinal = -1;
            GrandchildOrdinals = null;
            RequiredIndex = -1;
        }
    }

    /// <summary>
    /// Pre-compiled property lookup table for O(n) UTF8 byte matching.
    /// Populated by SchemaIndexer after tree construction and ordinal assignment.
    /// Null for nodes without properties.
    /// </summary>
    internal PropertyEntry[]? PropertyLookup { get; set; }

    /// <summary>
    /// Number of entries in the Required array. Used to size the required-seen bitset.
    /// </summary>
    internal int RequiredCount => Required?.Length ?? 0;

    /// <summary>
    /// Pre-computed UTF8 bytes of required property names.
    /// Populated by SchemaIndexer to enable zero-allocation required tracking.
    /// </summary>
    internal byte[][]? RequiredUtf8 { get; set; }

    // ── Path building helper ─────────────────────────────────────────────

    /// <summary>
    /// Builds a child JSON Pointer path from a parent path and property name,
    /// applying RFC 6901 escaping (<c>~</c> → <c>~0</c>, <c>/</c> → <c>~1</c>).
    /// </summary>
    /// <param name="parentPath">The parent node's path (root is <c>""</c>).</param>
    /// <param name="propertyName">The child property or key name.</param>
    /// <returns>The full RFC 6901 JSON Pointer for the child.</returns>
    internal static string BuildChildPath(string parentPath, string propertyName)
    {
        // RFC 6901: encode ~ as ~0, / as ~1 (order matters — ~ first!)
        string escaped = propertyName.Replace("~", "~0").Replace("/", "~1");
        return parentPath.Length == 0
            ? "/" + escaped
            : parentPath + "/" + escaped;
    }
}
