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

using System.Text;
using Gluey.Contract;

namespace Gluey.Contract.Json;

/// <summary>
/// Result of a schema walk operation. Separates structural from validation concerns.
/// Not a ref struct so it can escape the walker's stack frame.
/// </summary>
internal struct WalkResult
{
    internal OffsetTable Table;
    internal ErrorCollector Errors;
    internal ArrayBuffer? ArrayBuffer;
    internal bool HasStructuralError;
}

/// <summary>
/// Internal ref struct that performs single-pass JSON validation and offset table construction.
/// Wraps <see cref="JsonByteReader"/> and dispatches to all keyword validators in one forward pass.
///
/// Performance-critical: designed for zero per-property heap allocation on the hot path.
/// Uses pre-compiled PropertyLookup tables for UTF8 byte matching (no string conversion),
/// stackalloc bitsets for required-property tracking, and bool-returning validation helpers
/// to avoid temporary ErrorCollector allocation.
/// </summary>
internal ref struct SchemaWalker
{
    private JsonByteReader _reader;
    private readonly SchemaNode _root;
    private readonly byte[]? _data;
    private readonly ReadOnlySpan<byte> _span;
    private ErrorCollector _errors;
    private OffsetTable _table;
    private readonly Dictionary<string, int>? _nameToOrdinal;
    private readonly bool _buildOffsets;
    private readonly bool _assertFormat;
    private bool _structuralError;
    private ArrayBuffer? _arrayBuffer;
    private Dictionary<string, ParsedProperty>? _capturedChildren;

    private SchemaWalker(
        ReadOnlySpan<byte> utf8Json,
        SchemaNode root,
        byte[]? data,
        Dictionary<string, int>? nameToOrdinal,
        int propertyCount,
        bool assertFormat)
    {
        _reader = new JsonByteReader(utf8Json);
        _root = root;
        _data = data;
        _span = utf8Json;
        _nameToOrdinal = nameToOrdinal;
        _buildOffsets = data is not null && nameToOrdinal is not null;
        _assertFormat = assertFormat;
        _structuralError = false;
        _errors = new ErrorCollector();
        _table = _buildOffsets ? new OffsetTable(propertyCount) : default;
        _arrayBuffer = _buildOffsets ? ArrayBuffer.Rent(16, propertyCount) : null;
        _capturedChildren = null;
    }

    // ── Static entry points ──────────────────────────────────────────────

    /// <summary>
    /// Walk with byte[] data -- full OffsetTable population.
    /// </summary>
    internal static WalkResult Walk(
        byte[] data,
        SchemaNode root,
        Dictionary<string, int> nameToOrdinal,
        int propertyCount,
        bool assertFormat)
    {
        var walker = new SchemaWalker(data, root, data, nameToOrdinal, propertyCount, assertFormat);
        return walker.Execute();
    }

    /// <summary>
    /// Walk with ReadOnlySpan -- validation only, no OffsetTable.
    /// </summary>
    internal static WalkResult Walk(
        ReadOnlySpan<byte> data,
        SchemaNode root,
        int propertyCount,
        bool assertFormat)
    {
        var walker = new SchemaWalker(data, root, null, null, propertyCount, assertFormat);
        return walker.Execute();
    }

    private WalkResult Execute()
    {
        if (!_reader.Read())
        {
            _structuralError = true;
            if (_reader.HasError)
            {
                _errors.Add(new ValidationError(
                    "",
                    ValidationErrorCode.InvalidJson,
                    ValidationErrorMessages.Get(ValidationErrorCode.InvalidJson)));
            }
        }
        else
        {
            WalkValue(_root, "");
        }

        return new WalkResult
        {
            Table = _table,
            Errors = _errors,
            ArrayBuffer = _arrayBuffer,
            HasStructuralError = _structuralError,
        };
    }

    // ── Core walk methods ────────────────────────────────────────────────

    /// <summary>
    /// Validates the current token (already read) against the given schema node.
    /// Returns true if the value passes all validations for this node.
    /// </summary>
    /// <param name="node">The schema node to validate against.</param>
    /// <param name="path">The RFC 6901 JSON Pointer path.</param>
    /// <param name="arrayOrdinal">
    /// When the current value is an array that should populate ArrayBuffer,
    /// this is the ordinal to use as the array key. -1 means no ArrayBuffer population.
    /// </param>
    private bool WalkValue(SchemaNode node, string path, int arrayOrdinal = -1)
    {
        // Follow $ref transparently
        var effective = node.ResolvedRef ?? node;

        // Boolean schema check
        if (effective.BooleanSchema.HasValue)
        {
            if (effective.BooleanSchema.Value)
            {
                // Boolean true: accept -- skip the value
                SkipCurrentValue();
                return true;
            }
            else
            {
                // Boolean false: reject everything
                _errors.Add(new ValidationError(
                    path,
                    ValidationErrorCode.TypeMismatch,
                    "Schema is false; no value is valid."));
                SkipCurrentValue();
                return false;
            }
        }

        var tokenType = _reader.TokenType;
        var byteOffset = _reader.ByteOffset;
        var byteLength = _reader.ByteLength;

        // Complex types: delegate to WalkObject/WalkArray
        if (tokenType == JsonByteTokenType.StartObject)
            return WalkObject(effective, path);

        if (tokenType == JsonByteTokenType.StartArray)
            return WalkArray(effective, path, arrayOrdinal);

        // Scalar validation
        return ValidateScalar(effective, tokenType, byteOffset, byteLength, path);
    }

    /// <summary>
    /// Validates a scalar value (number, string, bool, null) against the schema node.
    /// </summary>
    private bool ValidateScalar(SchemaNode node, JsonByteTokenType tokenType, int byteOffset, int byteLength, string path)
    {
        bool valid = true;
        bool isNumber = tokenType == JsonByteTokenType.Number;
        ReadOnlySpan<byte> valueBytes = GetValueBytes(byteOffset, byteLength);
        // For enum/const comparison, we need the full JSON representation (strings include quotes)
        ReadOnlySpan<byte> rawJsonBytes = GetRawJsonBytes(tokenType, byteOffset, byteLength);
        bool isInteger = isNumber && KeywordValidator.IsInteger(valueBytes);

        // 1. Type validation
        bool typeValid = true;
        if (node.Type.HasValue)
        {
            typeValid = KeywordValidator.ValidateType(node.Type.Value, tokenType, isInteger, path, _errors);
            if (!typeValid)
                valid = false;
        }

        // 2. Enum / Const (use raw JSON representation including quotes for strings)
        if (node.Enum is not null)
        {
            if (!KeywordValidator.ValidateEnum(node.Enum, rawJsonBytes, isNumber, path, _errors))
                valid = false;
        }
        if (node.Const is not null)
        {
            if (!KeywordValidator.ValidateConst(node.Const, rawJsonBytes, isNumber, path, _errors))
                valid = false;
        }

        // 3. Type-dependent constraints (only if type passed or type not specified)
        if (typeValid)
        {
            if (isNumber)
                ValidateNumericConstraints(node, valueBytes, path, ref valid);

            if (tokenType == JsonByteTokenType.String)
                ValidateStringConstraints(node, valueBytes, path, ref valid);
        }

        // 4. Format (if assertFormat)
        if (_assertFormat && node.Format is not null && tokenType == JsonByteTokenType.String)
        {
            if (!FormatValidator.Validate(node.Format, valueBytes, path, _errors))
                valid = false;
        }

        // 5. Composition
        ValidateCompositionForScalar(node, tokenType, isInteger, valueBytes, rawJsonBytes, path, ref valid);

        // 6. Conditionals
        ValidateConditionalsForScalar(node, tokenType, isInteger, valueBytes, rawJsonBytes, path, ref valid);

        return valid;
    }

    private void ValidateNumericConstraints(SchemaNode node, ReadOnlySpan<byte> valueBytes, string path, ref bool valid)
    {
        if (node.Minimum is null && node.Maximum is null &&
            node.ExclusiveMinimum is null && node.ExclusiveMaximum is null &&
            node.MultipleOf is null)
            return;

        if (!NumericValidator.TryParseDecimal(valueBytes, out decimal numValue))
            return;

        if (node.Minimum.HasValue && !NumericValidator.ValidateMinimum(numValue, node.Minimum.Value, path, _errors))
            valid = false;
        if (node.Maximum.HasValue && !NumericValidator.ValidateMaximum(numValue, node.Maximum.Value, path, _errors))
            valid = false;
        if (node.ExclusiveMinimum.HasValue && !NumericValidator.ValidateExclusiveMinimum(numValue, node.ExclusiveMinimum.Value, path, _errors))
            valid = false;
        if (node.ExclusiveMaximum.HasValue && !NumericValidator.ValidateExclusiveMaximum(numValue, node.ExclusiveMaximum.Value, path, _errors))
            valid = false;
        if (node.MultipleOf.HasValue && !NumericValidator.ValidateMultipleOf(numValue, node.MultipleOf.Value, path, _errors))
            valid = false;
    }

    private void ValidateStringConstraints(SchemaNode node, ReadOnlySpan<byte> valueBytes, string path, ref bool valid)
    {
        if (node.MinLength is null && node.MaxLength is null && node.CompiledPattern is null)
            return;

        int codepointCount = -1;

        if (node.MinLength.HasValue)
        {
            if (codepointCount < 0) codepointCount = StringValidator.CountCodepoints(valueBytes);
            if (!StringValidator.ValidateMinLength(codepointCount, node.MinLength.Value, path, _errors))
                valid = false;
        }
        if (node.MaxLength.HasValue)
        {
            if (codepointCount < 0) codepointCount = StringValidator.CountCodepoints(valueBytes);
            if (!StringValidator.ValidateMaxLength(codepointCount, node.MaxLength.Value, path, _errors))
                valid = false;
        }
        if (node.CompiledPattern is not null)
        {
            string stringValue = Encoding.UTF8.GetString(valueBytes);
            if (!StringValidator.ValidatePattern(stringValue, node.CompiledPattern, path, _errors))
                valid = false;
        }
    }

    // ── Object walking ───────────────────────────────────────────────────

    private bool WalkObject(SchemaNode node, string path)
    {
        bool valid = true;
        int propertyCount = 0;

        // Track the start of this object for byte range calculation
        int objectStartOffset = _reader.ByteOffset;

        // Required property tracking: use a stackalloc bitset instead of HashSet<string>
        int requiredCount = node.RequiredCount;
        Span<bool> requiredSeen = requiredCount <= 64
            ? stackalloc bool[requiredCount]
            : new bool[requiredCount];
        if (requiredCount > 0)
            requiredSeen.Clear();

        // For dependentRequired/dependentSchemas/propertyNames/composition, we may need
        // a HashSet<string> but ONLY if those features are actually used on this node.
        // Most schemas don't use these, so this avoids allocation in the common case.
        HashSet<string>? seenPropertyNames = null;
        if (node.DependentRequired is not null || node.DependentSchemas is not null ||
            node.PropertyNames is not null ||
            node.AllOf is not null || node.AnyOf is not null || node.OneOf is not null || node.Not is not null ||
            node.If is not null)
        {
            seenPropertyNames = new HashSet<string>();
        }

        // Pre-resolved property lookup table (pre-computed at schema load time)
        var lookup = (node.ResolvedRef ?? node).PropertyLookup;

        while (true)
        {
            if (!_reader.Read())
            {
                _structuralError = true;
                AddStructuralError(path);
                return false;
            }

            if (_reader.TokenType == JsonByteTokenType.EndObject)
                break;

            if (_reader.TokenType != JsonByteTokenType.PropertyName)
            {
                _structuralError = true;
                AddStructuralError(path);
                return false;
            }

            propertyCount++;

            // Get property name as UTF8 span (no string allocation)
            ReadOnlySpan<byte> nameBytes = GetValueBytes(_reader.ByteOffset, _reader.ByteLength);

            // Look up in pre-compiled PropertyLookup (UTF8 byte comparison, no string allocation)
            SchemaNode.PropertyEntry? matchedEntry = null;
            if (lookup is not null)
            {
                for (int i = 0; i < lookup.Length; i++)
                {
                    if (nameBytes.SequenceEqual(lookup[i].Utf8Name))
                    {
                        matchedEntry = lookup[i];
                        break;
                    }
                }
            }

            // Track required property (bitset, no allocation)
            if (matchedEntry is not null && matchedEntry.RequiredIndex >= 0)
            {
                requiredSeen[matchedEntry.RequiredIndex] = true;
            }

            // For features that need string names, decode only when needed
            string? name = null;
            string? childPath = null;
            bool isKnownProperty = matchedEntry is not null;

            if (isKnownProperty)
            {
                // Known property: use pre-computed strings (no allocation)
                name = matchedEntry!.Name;
                childPath = matchedEntry.ChildPath;
            }

            // Find child schema
            SchemaNode? childSchema = matchedEntry?.Child;

            // For unknown properties, check required using pre-computed UTF8 bytes (no string allocation)
            if (!isKnownProperty && node.RequiredUtf8 is not null)
            {
                for (int r = 0; r < node.RequiredUtf8.Length; r++)
                {
                    if (nameBytes.SequenceEqual(node.RequiredUtf8[r]))
                    {
                        requiredSeen[r] = true;
                        break;
                    }
                }
            }

            // For features that need string names, decode only when needed
            bool needsNameString = !isKnownProperty && (
                seenPropertyNames is not null ||
                (node.AdditionalProperties is not null && node.AdditionalProperties.BooleanSchema == false) ||
                node.CompiledPatternProperties is not null);

            if (!isKnownProperty && needsNameString)
            {
                name = Encoding.UTF8.GetString(nameBytes);
            }

            // Add to seenPropertyNames only when needed (for advanced features)
            if (seenPropertyNames is not null)
            {
                name ??= Encoding.UTF8.GetString(nameBytes);
                seenPropertyNames.Add(name);
            }

            // Check additionalProperties for unknown properties
            if (childSchema is null && node.AdditionalProperties is not null
                && node.AdditionalProperties.BooleanSchema == false)
            {
                // Need name for error message
                name ??= Encoding.UTF8.GetString(nameBytes);
                childPath ??= SchemaNode.BuildChildPath(path, name);
                if (!KeywordValidator.ValidateAdditionalProperty(name, node.Properties, node.AdditionalProperties, path, _errors))
                    valid = false;
            }

            // Check patternProperties
            SchemaNode? patternSchema = null;
            if (node.CompiledPatternProperties is not null)
            {
                name ??= Encoding.UTF8.GetString(nameBytes);
                foreach (var (pattern, schema) in node.CompiledPatternProperties)
                {
                    if (pattern.IsMatch(name))
                    {
                        patternSchema = schema;
                        break;
                    }
                }
            }

            // Read the value token
            if (!_reader.Read())
            {
                _structuralError = true;
                AddStructuralError(path);
                return false;
            }

            // Record offset before walking (for OffsetTable)
            int valueOffset = _reader.ByteOffset;
            int valueLength = _reader.ByteLength;

            // Resolve ordinal from pre-compiled entry or nameToOrdinal lookup
            int resolvedChildOrdinal = matchedEntry?.Ordinal ?? -1;
            if (resolvedChildOrdinal < 0 && _buildOffsets && childPath is not null
                && _nameToOrdinal!.TryGetValue(childPath, out int fallbackOrd))
            {
                resolvedChildOrdinal = fallbackOrd;
            }

            // Determine effective schema for the value
            var effectiveSchema = childSchema ?? patternSchema;

            // Determine if this child is an array (pass ordinal for ArrayBuffer population)
            int childArrayOrd = -1;
            if (_buildOffsets && resolvedChildOrdinal >= 0 && effectiveSchema is not null)
            {
                var resolvedEffective = effectiveSchema.ResolvedRef ?? effectiveSchema;
                if (resolvedEffective.Items is not null || resolvedEffective.PrefixItems is not null ||
                    (resolvedEffective.Type.HasValue && (resolvedEffective.Type.Value & SchemaType.Array) != 0))
                {
                    childArrayOrd = resolvedChildOrdinal;
                }
            }

            if (effectiveSchema is not null)
            {
                // If additionalProperties is a schema (not boolean false/true), validate against it
                // for unknown properties
                if (childSchema is null && node.AdditionalProperties is not null
                    && node.AdditionalProperties.BooleanSchema != false
                    && node.AdditionalProperties.BooleanSchema != true
                    && patternSchema is null)
                {
                    effectiveSchema = node.AdditionalProperties;
                }

                // Fast path: for unknown properties with simple schemas, try to validate
                // without allocating path strings. Only allocate if validation fails.
                if (!isKnownProperty && childPath is null && TryWalkValueFastPath(effectiveSchema, childArrayOrd))
                {
                    // Validated successfully without path allocation
                }
                else
                {
                    childPath ??= LazyBuildChildPath(path, name, nameBytes);
                    if (!WalkValue(effectiveSchema, childPath, childArrayOrd))
                        valid = false;
                }
            }
            else if (node.AdditionalProperties is not null
                     && node.AdditionalProperties.BooleanSchema != false
                     && node.AdditionalProperties.BooleanSchema != true)
            {
                // additionalProperties is a schema -- validate against it
                if (!isKnownProperty && childPath is null && TryWalkValueFastPath(node.AdditionalProperties, -1))
                {
                    // Validated successfully without path allocation
                }
                else
                {
                    childPath ??= LazyBuildChildPath(path, name, nameBytes);
                    if (!WalkValue(node.AdditionalProperties, childPath))
                        valid = false;
                }
            }
            else
            {
                // No schema applies -- skip the value (already read the first token)
                SkipCurrentValue();
            }

            // Store in OffsetTable if applicable
            if (_buildOffsets && resolvedChildOrdinal >= 0)
            {
                childPath ??= LazyBuildChildPath(path, name, nameBytes);

                // Use pre-computed grandchild ordinals from PropertyEntry (no per-parse allocation)
                Dictionary<string, int>? grandchildOrdinals = matchedEntry?.GrandchildOrdinals;

                // If not from lookup, fall back to computing for unknown properties with schemas
                if (grandchildOrdinals is null && matchedEntry is null)
                {
                    var resolvedChild = (childSchema?.ResolvedRef ?? childSchema);
                    if (resolvedChild?.Properties is not null)
                    {
                        grandchildOrdinals = new Dictionary<string, int>();
                        foreach (var (gcName, _) in resolvedChild.Properties)
                        {
                            string gcPath = SchemaNode.BuildChildPath(childPath, gcName);
                            if (_nameToOrdinal!.TryGetValue(gcPath, out int gcOrd))
                            {
                                grandchildOrdinals[gcName] = gcOrd;
                            }
                        }
                    }
                }

                // Check if this child is an array type with items
                ArrayBuffer? childArrayBuffer = null;
                int childArrayOrdinal = -1;
                var resolvedChildNode = (childSchema?.ResolvedRef ?? childSchema);
                if (resolvedChildNode is not null &&
                    (resolvedChildNode.Items is not null || resolvedChildNode.PrefixItems is not null) &&
                    _arrayBuffer is not null)
                {
                    childArrayBuffer = _arrayBuffer;
                    childArrayOrdinal = resolvedChildOrdinal;
                }

                var property = new ParsedProperty(_data!, valueOffset, valueLength, childPath,
                    _table, grandchildOrdinals, childArrayBuffer, childArrayOrdinal);
                _table.Set(resolvedChildOrdinal, property);
            }

            // Also capture for array element child snapshotting if active
            if (_capturedChildren is not null)
            {
                childPath ??= LazyBuildChildPath(path, name, nameBytes);
                name ??= Encoding.UTF8.GetString(nameBytes);
                var property = new ParsedProperty(_data!, valueOffset, valueLength, childPath);
                _capturedChildren[name] = property;
            }
        }

        // Post-object validations: required (using bitset, no HashSet lookup)
        if (requiredCount > 0)
        {
            for (int r = 0; r < requiredCount; r++)
            {
                if (!requiredSeen[r])
                {
                    _errors.Add(new ValidationError(
                        SchemaNode.BuildChildPath(path, node.Required![r]),
                        ValidationErrorCode.RequiredMissing,
                        ValidationErrorMessages.Get(ValidationErrorCode.RequiredMissing)));
                    valid = false;
                }
            }
        }

        if (node.MinProperties.HasValue)
        {
            if (!ObjectValidator.ValidateMinProperties(propertyCount, node.MinProperties.Value, path, _errors))
                valid = false;
        }

        if (node.MaxProperties.HasValue)
        {
            if (!ObjectValidator.ValidateMaxProperties(propertyCount, node.MaxProperties.Value, path, _errors))
                valid = false;
        }

        // DependentRequired
        if (node.DependentRequired is not null && seenPropertyNames is not null)
        {
            if (!DependencyValidator.ValidateDependentRequired(node.DependentRequired, seenPropertyNames, path, _errors))
                valid = false;
        }

        // DependentSchemas
        if (node.DependentSchemas is not null && seenPropertyNames is not null)
        {
            foreach (var (trigger, schema) in node.DependentSchemas)
            {
                if (seenPropertyNames.Contains(trigger))
                {
                    // For dependent schemas, we validate structural constraints
                    // (required, minProperties etc.) from the subschema against captured state
                    bool subValid = ValidateObjectSubschema(schema, seenPropertyNames, propertyCount, path);
                    if (!DependencyValidator.ValidateDependentSchema(subValid, path, _errors))
                        valid = false;
                }
            }
        }

        // PropertyNames validation
        if (node.PropertyNames is not null && seenPropertyNames is not null)
        {
            foreach (var propName in seenPropertyNames)
            {
                bool nameValid = ValidatePropertyName(node.PropertyNames, propName, path);
                if (!ObjectValidator.ValidatePropertyName(nameValid, propName, path, _errors))
                    valid = false;
            }
        }

        // Composition at object level
        if (seenPropertyNames is not null)
        {
            ValidateCompositionForObject(node, seenPropertyNames, propertyCount, path, ref valid);

            // Conditionals at object level
            ValidateConditionalsForObject(node, seenPropertyNames, propertyCount, path, ref valid);
        }

        return valid;
    }

    // ── Array walking ────────────────────────────────────────────────────

    /// <summary>
    /// Walks an array, validating elements and optionally populating ArrayBuffer.
    /// The parentArrayOrdinal parameter allows the parent to specify which ordinal
    /// to use for storing elements in the ArrayBuffer.
    /// </summary>
    private bool WalkArray(SchemaNode node, string path, int parentArrayOrdinal = -1)
    {
        bool valid = true;
        int elementCount = 0;
        int containsMatchCount = 0;

        // For uniqueItems
        List<byte[]>? elementBytesList = null;
        List<bool>? isNumberList = null;
        if (node.UniqueItems == true)
        {
            elementBytesList = new List<byte[]>();
            isNumberList = new List<bool>();
        }

        while (true)
        {
            if (!_reader.Read())
            {
                _structuralError = true;
                AddStructuralError(path);
                return false;
            }

            if (_reader.TokenType == JsonByteTokenType.EndArray)
                break;

            string elementPath = path + "/" + elementCount;

            // Capture element bytes for uniqueItems before walking
            int elemOffset = _reader.ByteOffset;
            int elemLength = _reader.ByteLength;
            bool elemIsNumber = _reader.TokenType == JsonByteTokenType.Number;

            // Get item schema
            var itemSchema = KeywordValidator.GetItemSchema(elementCount, node.PrefixItems, node.Items);
            var effectiveItemSchema = itemSchema ?? SchemaNode.True;

            // Enable child capture for array elements that might be objects
            if (_buildOffsets && parentArrayOrdinal >= 0)
            {
                var resolvedItem = effectiveItemSchema.ResolvedRef ?? effectiveItemSchema;
                if (resolvedItem.Properties is not null)
                {
                    _capturedChildren = new Dictionary<string, ParsedProperty>();
                }
            }

            if (!WalkValue(effectiveItemSchema, elementPath))
                valid = false;

            // Store element in ArrayBuffer for indexed access
            if (_buildOffsets && _arrayBuffer is not null && parentArrayOrdinal >= 0)
            {
                ParsedProperty elemProperty;
                if (_capturedChildren is not null && _capturedChildren.Count > 0)
                {
                    // Use captured children from WalkObject (snapshotted during walk)
                    elemProperty = new ParsedProperty(_data!, elemOffset, elemLength, elementPath,
                        _capturedChildren);
                    _capturedChildren = null; // consumed -- next element will create a new one
                }
                else
                {
                    elemProperty = new ParsedProperty(_data!, elemOffset, elemLength, elementPath);
                }
                _arrayBuffer.Add(parentArrayOrdinal, elemProperty);
            }

            // Contains check
            if (node.Contains is not null)
            {
                bool containsValid = ValidateValueAgainstSchemaNoErrors(node.Contains, _reader.TokenType, elemOffset, elemLength, elementPath);
                if (containsValid)
                    containsMatchCount++;
            }

            // UniqueItems: capture element bytes
            if (elementBytesList is not null && _data is not null)
            {
                var bytes = new byte[elemLength];
                Array.Copy(_data, elemOffset, bytes, 0, elemLength);
                elementBytesList.Add(bytes);
                isNumberList!.Add(elemIsNumber);
            }

            elementCount++;
        }

        // Post-array validations
        if (node.MinItems.HasValue)
        {
            if (!ArrayValidator.ValidateMinItems(elementCount, node.MinItems.Value, path, _errors))
                valid = false;
        }

        if (node.MaxItems.HasValue)
        {
            if (!ArrayValidator.ValidateMaxItems(elementCount, node.MaxItems.Value, path, _errors))
                valid = false;
        }

        if (node.Contains is not null)
        {
            if (!ArrayValidator.ValidateContains(containsMatchCount, node.MinContains, node.MaxContains, path, _errors))
                valid = false;
        }

        if (node.UniqueItems == true && elementBytesList is not null)
        {
            if (!ArrayValidator.ValidateUniqueItems(elementBytesList.ToArray(), isNumberList!.ToArray(), path, _errors))
                valid = false;
        }

        // Composition at array level
        ValidateCompositionForArray(node, elementCount, path, ref valid);

        return valid;
    }

    // ── Composition helpers ──────────────────────────────────────────────

    private void ValidateCompositionForScalar(
        SchemaNode node, JsonByteTokenType tokenType, bool isInteger,
        ReadOnlySpan<byte> valueBytes, ReadOnlySpan<byte> rawJsonBytes, string path, ref bool valid)
    {
        if (node.AllOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AllOf)
            {
                if (ValidateScalarAgainstSchema(sub, tokenType, isInteger, valueBytes, rawJsonBytes, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAllOf(passCount, node.AllOf.Length, path, _errors))
                valid = false;
        }

        if (node.AnyOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AnyOf)
            {
                if (ValidateScalarAgainstSchema(sub, tokenType, isInteger, valueBytes, rawJsonBytes, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAnyOf(passCount, path, _errors))
                valid = false;
        }

        if (node.OneOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.OneOf)
            {
                if (ValidateScalarAgainstSchema(sub, tokenType, isInteger, valueBytes, rawJsonBytes, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateOneOf(passCount, path, _errors))
                valid = false;
        }

        if (node.Not is not null)
        {
            bool subResult = ValidateScalarAgainstSchema(node.Not, tokenType, isInteger, valueBytes, rawJsonBytes, path);
            if (!CompositionValidator.ValidateNot(subResult, path, _errors))
                valid = false;
        }
    }

    private void ValidateConditionalsForScalar(
        SchemaNode node, JsonByteTokenType tokenType, bool isInteger,
        ReadOnlySpan<byte> valueBytes, ReadOnlySpan<byte> rawJsonBytes, string path, ref bool valid)
    {
        if (node.If is null)
            return;

        bool ifResult = ValidateScalarAgainstSchema(node.If, tokenType, isInteger, valueBytes, rawJsonBytes, path);
        if (ifResult && node.Then is not null)
        {
            bool thenResult = ValidateScalarAgainstSchema(node.Then, tokenType, isInteger, valueBytes, rawJsonBytes, path);
            if (!ConditionalValidator.ValidateIfThen(thenResult, path, _errors))
                valid = false;
        }
        else if (!ifResult && node.Else is not null)
        {
            bool elseResult = ValidateScalarAgainstSchema(node.Else, tokenType, isInteger, valueBytes, rawJsonBytes, path);
            if (!ConditionalValidator.ValidateIfElse(elseResult, path, _errors))
                valid = false;
        }
    }

    /// <summary>
    /// Validates a scalar value against a subschema without re-reading tokens.
    /// Uses no temporary ErrorCollector -- only returns bool indicating pass/fail.
    /// </summary>
    private bool ValidateScalarAgainstSchema(
        SchemaNode subNode, JsonByteTokenType tokenType, bool isInteger,
        ReadOnlySpan<byte> valueBytes, ReadOnlySpan<byte> rawJsonBytes, string path)
    {
        var effective = subNode.ResolvedRef ?? subNode;

        if (effective.BooleanSchema.HasValue)
            return effective.BooleanSchema.Value;

        // Type
        if (effective.Type.HasValue)
        {
            if (!KeywordValidator.CheckType(effective.Type.Value, tokenType, isInteger))
                return false;
        }

        // Enum/Const (use rawJsonBytes for proper comparison)
        bool isNumber = tokenType == JsonByteTokenType.Number;
        if (effective.Enum is not null)
        {
            if (!KeywordValidator.CheckEnum(effective.Enum, rawJsonBytes, isNumber))
                return false;
        }
        if (effective.Const is not null)
        {
            if (!KeywordValidator.CheckConst(effective.Const, rawJsonBytes, isNumber))
                return false;
        }

        // Numeric constraints
        if (isNumber)
        {
            if (effective.Minimum is not null || effective.Maximum is not null ||
                effective.ExclusiveMinimum is not null || effective.ExclusiveMaximum is not null ||
                effective.MultipleOf is not null)
            {
                if (NumericValidator.TryParseDecimal(valueBytes, out decimal numValue))
                {
                    if (effective.Minimum.HasValue && numValue < effective.Minimum.Value)
                        return false;
                    if (effective.Maximum.HasValue && numValue > effective.Maximum.Value)
                        return false;
                    if (effective.ExclusiveMinimum.HasValue && numValue <= effective.ExclusiveMinimum.Value)
                        return false;
                    if (effective.ExclusiveMaximum.HasValue && numValue >= effective.ExclusiveMaximum.Value)
                        return false;
                    if (effective.MultipleOf.HasValue && numValue % effective.MultipleOf.Value != 0)
                        return false;
                }
            }
        }

        // String constraints
        if (tokenType == JsonByteTokenType.String)
        {
            if (effective.MinLength is not null || effective.MaxLength is not null || effective.CompiledPattern is not null)
            {
                int codepointCount = -1;
                if (effective.MinLength.HasValue)
                {
                    codepointCount = StringValidator.CountCodepoints(valueBytes);
                    if (codepointCount < effective.MinLength.Value)
                        return false;
                }
                if (effective.MaxLength.HasValue)
                {
                    if (codepointCount < 0) codepointCount = StringValidator.CountCodepoints(valueBytes);
                    if (codepointCount > effective.MaxLength.Value)
                        return false;
                }
                if (effective.CompiledPattern is not null)
                {
                    string strValue = Encoding.UTF8.GetString(valueBytes);
                    if (!effective.CompiledPattern.IsMatch(strValue))
                        return false;
                }
            }
        }

        // Format
        if (_assertFormat && effective.Format is not null && tokenType == JsonByteTokenType.String)
        {
            if (!FormatValidator.Check(effective.Format, valueBytes))
                return false;
        }

        return true;
    }

    // ── Object composition/conditional helpers ───────────────────────────

    private void ValidateCompositionForObject(
        SchemaNode node, HashSet<string> seenProperties, int propertyCount,
        string path, ref bool valid)
    {
        if (node.AllOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AllOf)
            {
                if (ValidateObjectSubschema(sub, seenProperties, propertyCount, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAllOf(passCount, node.AllOf.Length, path, _errors))
                valid = false;
        }

        if (node.AnyOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AnyOf)
            {
                if (ValidateObjectSubschema(sub, seenProperties, propertyCount, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAnyOf(passCount, path, _errors))
                valid = false;
        }

        if (node.OneOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.OneOf)
            {
                if (ValidateObjectSubschema(sub, seenProperties, propertyCount, path))
                    passCount++;
            }
            if (!CompositionValidator.ValidateOneOf(passCount, path, _errors))
                valid = false;
        }

        if (node.Not is not null)
        {
            bool subResult = ValidateObjectSubschema(node.Not, seenProperties, propertyCount, path);
            if (!CompositionValidator.ValidateNot(subResult, path, _errors))
                valid = false;
        }
    }

    private void ValidateConditionalsForObject(
        SchemaNode node, HashSet<string> seenProperties, int propertyCount,
        string path, ref bool valid)
    {
        if (node.If is null)
            return;

        bool ifResult = ValidateObjectSubschema(node.If, seenProperties, propertyCount, path);
        if (ifResult && node.Then is not null)
        {
            bool thenResult = ValidateObjectSubschema(node.Then, seenProperties, propertyCount, path);
            if (!ConditionalValidator.ValidateIfThen(thenResult, path, _errors))
                valid = false;
        }
        else if (!ifResult && node.Else is not null)
        {
            bool elseResult = ValidateObjectSubschema(node.Else, seenProperties, propertyCount, path);
            if (!ConditionalValidator.ValidateIfElse(elseResult, path, _errors))
                valid = false;
        }
    }

    /// <summary>
    /// Validates an object's captured state against a subschema without re-reading tokens.
    /// Uses no temporary ErrorCollector -- only returns bool indicating pass/fail.
    /// </summary>
    private bool ValidateObjectSubschema(
        SchemaNode subNode, HashSet<string> seenProperties, int propertyCount, string path)
    {
        var effective = subNode.ResolvedRef ?? subNode;

        if (effective.BooleanSchema.HasValue)
            return effective.BooleanSchema.Value;

        // Type check for object
        if (effective.Type.HasValue)
        {
            if (!KeywordValidator.CheckType(effective.Type.Value, JsonByteTokenType.StartObject, false))
                return false;
        }

        // Required
        if (effective.Required is not null)
        {
            foreach (var req in effective.Required)
            {
                if (!seenProperties.Contains(req))
                    return false;
            }
        }

        // MinProperties / MaxProperties
        if (effective.MinProperties.HasValue && propertyCount < effective.MinProperties.Value)
            return false;
        if (effective.MaxProperties.HasValue && propertyCount > effective.MaxProperties.Value)
            return false;

        // DependentRequired
        if (effective.DependentRequired is not null)
        {
            foreach (var (trigger, deps) in effective.DependentRequired)
            {
                if (seenProperties.Contains(trigger))
                {
                    foreach (var dep in deps)
                    {
                        if (!seenProperties.Contains(dep))
                            return false;
                    }
                }
            }
        }

        return true;
    }

    // ── Array composition helpers ────────────────────────────────────────

    private void ValidateCompositionForArray(
        SchemaNode node, int elementCount, string path, ref bool valid)
    {
        if (node.AllOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AllOf)
            {
                if (ValidateArraySubschema(sub, elementCount))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAllOf(passCount, node.AllOf.Length, path, _errors))
                valid = false;
        }

        if (node.AnyOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.AnyOf)
            {
                if (ValidateArraySubschema(sub, elementCount))
                    passCount++;
            }
            if (!CompositionValidator.ValidateAnyOf(passCount, path, _errors))
                valid = false;
        }

        if (node.OneOf is not null)
        {
            int passCount = 0;
            foreach (var sub in node.OneOf)
            {
                if (ValidateArraySubschema(sub, elementCount))
                    passCount++;
            }
            if (!CompositionValidator.ValidateOneOf(passCount, path, _errors))
                valid = false;
        }

        if (node.Not is not null)
        {
            bool subResult = ValidateArraySubschema(node.Not, elementCount);
            if (!CompositionValidator.ValidateNot(subResult, path, _errors))
                valid = false;
        }
    }

    /// <summary>
    /// Validates array state against a subschema without allocation.
    /// Returns bool only -- no ErrorCollector needed.
    /// </summary>
    private bool ValidateArraySubschema(SchemaNode subNode, int elementCount)
    {
        var effective = subNode.ResolvedRef ?? subNode;

        if (effective.BooleanSchema.HasValue)
            return effective.BooleanSchema.Value;

        if (effective.Type.HasValue)
        {
            if (!KeywordValidator.CheckType(effective.Type.Value, JsonByteTokenType.StartArray, false))
                return false;
        }

        if (effective.MinItems.HasValue && elementCount < effective.MinItems.Value)
            return false;
        if (effective.MaxItems.HasValue && elementCount > effective.MaxItems.Value)
            return false;

        return true;
    }

    // ── PropertyNames helper ─────────────────────────────────────────────

    /// <summary>
    /// Validates a property name against a schema without allocation.
    /// Returns bool only -- no ErrorCollector needed.
    /// </summary>
    private bool ValidatePropertyName(SchemaNode nameSchema, string name, string path)
    {
        var effective = nameSchema.ResolvedRef ?? nameSchema;

        if (effective.BooleanSchema.HasValue)
            return effective.BooleanSchema.Value;

        // PropertyNames typically validates the name as a string value
        if (effective.Type.HasValue)
        {
            if (!KeywordValidator.CheckType(effective.Type.Value, JsonByteTokenType.String, false))
                return false;
        }

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        if (effective.MinLength.HasValue)
        {
            int cp = StringValidator.CountCodepoints(nameBytes);
            if (cp < effective.MinLength.Value)
                return false;
        }
        if (effective.MaxLength.HasValue)
        {
            int cp = StringValidator.CountCodepoints(nameBytes);
            if (cp > effective.MaxLength.Value)
                return false;
        }
        if (effective.CompiledPattern is not null)
        {
            if (!effective.CompiledPattern.IsMatch(name))
                return false;
        }

        return true;
    }

    // ── Contains helper (no-error validation) ────────────────────────────

    /// <summary>
    /// Validates a value against a schema for contains checking.
    /// No allocation -- returns bool only.
    /// </summary>
    private bool ValidateValueAgainstSchemaNoErrors(
        SchemaNode subNode, JsonByteTokenType tokenType, int byteOffset, int byteLength, string path)
    {
        var effective = subNode.ResolvedRef ?? subNode;

        if (effective.BooleanSchema.HasValue)
            return effective.BooleanSchema.Value;

        // For contains, we just check type match as a quick validation
        if (effective.Type.HasValue)
        {
            bool isNumber = tokenType == JsonByteTokenType.Number;
            ReadOnlySpan<byte> valueBytes = GetValueBytes(byteOffset, byteLength);
            bool isInteger = isNumber && KeywordValidator.IsInteger(valueBytes);

            if (!KeywordValidator.CheckType(effective.Type.Value, tokenType, isInteger))
                return false;
        }

        return true;
    }

    // ── Skip value utility ───────────────────────────────────────────────

    /// <summary>
    /// Skips the current JSON value (handles nested objects/arrays by tracking depth).
    /// Assumes the current token is already read (the opening token of the value).
    /// </summary>
    private void SkipCurrentValue()
    {
        var tokenType = _reader.TokenType;

        if (tokenType != JsonByteTokenType.StartObject && tokenType != JsonByteTokenType.StartArray)
            return; // Scalar -- already consumed

        int depth = 1;
        while (depth > 0)
        {
            if (!_reader.Read())
            {
                _structuralError = true;
                return;
            }

            if (_reader.TokenType == JsonByteTokenType.StartObject || _reader.TokenType == JsonByteTokenType.StartArray)
                depth++;
            else if (_reader.TokenType == JsonByteTokenType.EndObject || _reader.TokenType == JsonByteTokenType.EndArray)
                depth--;
        }
    }

    // ── Utility ──────────────────────────────────────────────────────────

    private ReadOnlySpan<byte> GetValueBytes(int offset, int length)
    {
        return _span.Slice(offset, length);
    }

    /// <summary>
    /// Gets the full JSON representation bytes for a token.
    /// For strings, this includes the surrounding quotes (offset-1, length+2).
    /// For other types, same as GetValueBytes.
    /// </summary>
    private ReadOnlySpan<byte> GetRawJsonBytes(JsonByteTokenType tokenType, int offset, int length)
    {
        if (tokenType == JsonByteTokenType.String || tokenType == JsonByteTokenType.PropertyName)
        {
            // ByteOffset for strings points to content inside quotes (+1 from quote)
            // We need to include the surrounding quotes for enum/const comparison
            return _span.Slice(offset - 1, length + 2);
        }

        // For booleans: true/false/null raw text matches directly
        if (tokenType == JsonByteTokenType.True)
            return "true"u8;
        if (tokenType == JsonByteTokenType.False)
            return "false"u8;
        if (tokenType == JsonByteTokenType.Null)
            return "null"u8;

        return _span.Slice(offset, length);
    }

    /// <summary>
    /// Fast-path validation for scalar values that avoids path string allocation.
    /// Returns true if the value was successfully validated without needing a path.
    /// Returns false if full validation with path is needed (complex type or validation failure).
    /// </summary>
    private bool TryWalkValueFastPath(SchemaNode node, int arrayOrdinal)
    {
        var effective = node.ResolvedRef ?? node;

        // Boolean schemas
        if (effective.BooleanSchema.HasValue)
        {
            if (effective.BooleanSchema.Value)
            {
                SkipCurrentValue();
                return true;
            }
            return false; // Need full path for error reporting
        }

        var tokenType = _reader.TokenType;

        // Only handle scalar tokens on fast path (objects/arrays need recursive walking with paths)
        if (tokenType == JsonByteTokenType.StartObject || tokenType == JsonByteTokenType.StartArray)
            return false;

        // Check if this is a simple type-only schema (most common for additionalProperties)
        // For simple schemas, we can validate without any string allocation
        if (effective.Type.HasValue)
        {
            bool isNumber = tokenType == JsonByteTokenType.Number;
            ReadOnlySpan<byte> valueBytes = GetValueBytes(_reader.ByteOffset, _reader.ByteLength);
            bool isInteger = isNumber && KeywordValidator.IsInteger(valueBytes);

            if (!KeywordValidator.CheckType(effective.Type.Value, tokenType, isInteger))
                return false; // Type mismatch -- need full path for error

            // If there are additional constraints beyond type, fall back to full validation
            if (effective.Enum is not null || effective.Const is not null ||
                effective.Minimum is not null || effective.Maximum is not null ||
                effective.ExclusiveMinimum is not null || effective.ExclusiveMaximum is not null ||
                effective.MultipleOf is not null ||
                effective.MinLength is not null || effective.MaxLength is not null ||
                effective.CompiledPattern is not null ||
                effective.AllOf is not null || effective.AnyOf is not null ||
                effective.OneOf is not null || effective.Not is not null ||
                effective.If is not null ||
                (_assertFormat && effective.Format is not null && tokenType == JsonByteTokenType.String))
            {
                return false; // Has additional constraints -- need full validation
            }

            // Type-only schema, type matched -- success!
            return true;
        }

        // No type constraint at all -- schema without type validates everything
        if (effective.Enum is null && effective.Const is null &&
            effective.AllOf is null && effective.AnyOf is null &&
            effective.OneOf is null && effective.Not is null && effective.If is null)
        {
            return true; // Empty schema -- accepts anything
        }

        return false; // Has non-type constraints -- need full validation
    }

    /// <summary>
    /// Builds a child path lazily, converting name bytes to string if needed.
    /// Avoids string allocation when name was already decoded.
    /// </summary>
    private static string LazyBuildChildPath(string parentPath, string? name, ReadOnlySpan<byte> nameBytes)
    {
        name ??= Encoding.UTF8.GetString(nameBytes);
        return SchemaNode.BuildChildPath(parentPath, name);
    }

    private void AddStructuralError(string path)
    {
        _errors.Add(new ValidationError(
            path,
            ValidationErrorCode.InvalidJson,
            ValidationErrorMessages.Get(ValidationErrorCode.InvalidJson)));
    }
}
