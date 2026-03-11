// Copyright 2025 Rebels Software sp. z o.o.
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
/// Machine-readable error codes for JSON Schema Draft 2020-12 validation failures.
/// One value per JSON Schema keyword.
/// </summary>
public enum ValidationErrorCode : byte
{
    // -- General --

    /// <summary>No error.</summary>
    None = 0,

    // -- Type (type keyword) --

    /// <summary>Value does not match the expected type.</summary>
    TypeMismatch,

    // -- Enum/Const keywords --

    /// <summary>Value is not one of the allowed enum values.</summary>
    EnumMismatch,

    /// <summary>Value does not match the const value.</summary>
    ConstMismatch,

    // -- Object keywords (required, additionalProperties) --

    /// <summary>A required property is missing.</summary>
    RequiredMissing,

    /// <summary>An additional property is not allowed by the schema.</summary>
    AdditionalPropertyNotAllowed,

    // -- Array keywords (items, prefixItems) --

    /// <summary>An array item does not match the items schema.</summary>
    ItemsInvalid,

    /// <summary>An array item does not match its positional prefixItems schema.</summary>
    PrefixItemsInvalid,

    // -- Numeric constraints --

    /// <summary>Value is less than the minimum.</summary>
    MinimumExceeded,

    /// <summary>Value is greater than the maximum.</summary>
    MaximumExceeded,

    /// <summary>Value is less than or equal to the exclusive minimum.</summary>
    ExclusiveMinimumExceeded,

    /// <summary>Value is greater than or equal to the exclusive maximum.</summary>
    ExclusiveMaximumExceeded,

    /// <summary>Value is not a multiple of the specified divisor.</summary>
    MultipleOfInvalid,

    // -- String constraints --

    /// <summary>String length is less than the minimum length.</summary>
    MinLengthExceeded,

    /// <summary>String length exceeds the maximum length.</summary>
    MaxLengthExceeded,

    /// <summary>String does not match the required pattern.</summary>
    PatternMismatch,

    // -- Array/Object size constraints --

    /// <summary>Array has fewer items than the minimum.</summary>
    MinItemsExceeded,

    /// <summary>Array has more items than the maximum.</summary>
    MaxItemsExceeded,

    /// <summary>Object has fewer properties than the minimum.</summary>
    MinPropertiesExceeded,

    /// <summary>Object has more properties than the maximum.</summary>
    MaxPropertiesExceeded,

    // -- Composition keywords --

    /// <summary>Value does not match all of the allOf schemas.</summary>
    AllOfInvalid,

    /// <summary>Value does not match any of the anyOf schemas.</summary>
    AnyOfInvalid,

    /// <summary>Value does not match exactly one of the oneOf schemas.</summary>
    OneOfInvalid,

    /// <summary>Value matches the not schema (should not match).</summary>
    NotInvalid,

    // -- Conditional keywords --

    /// <summary>Value matched the if schema but failed the then schema.</summary>
    IfThenInvalid,

    /// <summary>Value did not match the if schema and failed the else schema.</summary>
    IfElseInvalid,

    /// <summary>A dependent required property is missing.</summary>
    DependentRequiredMissing,

    /// <summary>Value does not match the dependent schema.</summary>
    DependentSchemaInvalid,

    // -- Advanced keywords --

    /// <summary>A property matching a pattern does not validate against the pattern property schema.</summary>
    PatternPropertyInvalid,

    /// <summary>A property name does not match the propertyNames schema.</summary>
    PropertyNameInvalid,

    /// <summary>Array does not contain an item matching the contains schema.</summary>
    ContainsInvalid,

    /// <summary>Array contains fewer matching items than the minimum required by minContains.</summary>
    MinContainsExceeded,

    /// <summary>Array contains more matching items than allowed by maxContains.</summary>
    MaxContainsExceeded,

    /// <summary>Array items are not unique as required by uniqueItems.</summary>
    UniqueItemsViolation,

    // -- Format keyword --

    /// <summary>Value does not match the expected format.</summary>
    FormatInvalid,

    // -- Schema reference errors --

    /// <summary>A $ref creates a circular reference chain.</summary>
    RefCycle,

    /// <summary>A $ref target cannot be resolved (missing $defs entry, bad JSON Pointer, or unregistered URI).</summary>
    RefUnresolved,

    /// <summary>A $anchor target cannot be resolved.</summary>
    AnchorUnresolved,

    /// <summary>Duplicate $anchor declaration in the same schema resource.</summary>
    AnchorDuplicate,

    // -- Structural errors --

    /// <summary>The input is not valid JSON (structural/syntax error).</summary>
    InvalidJson,

    // -- Sentinel --

    /// <summary>Too many validation errors; remaining errors have been truncated.</summary>
    TooManyErrors,
}
