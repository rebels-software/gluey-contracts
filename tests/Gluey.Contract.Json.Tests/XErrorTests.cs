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
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class XErrorTests
{
    private static ParseResult? Parse(string schemaJson, string dataJson)
    {
        var schema = JsonContractSchema.Load(schemaJson)!;
        return schema.Parse(Encoding.UTF8.GetBytes(dataJson));
    }

    // ── Parsing ──────────────────────────────────────────────────────────

    [Test]
    public void XError_ParsedIntoSchemaNode()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "maximum": 6,
                        "x-error": {
                            "code": "INVALID_QUANTITY",
                            "title": "Invalid quantity",
                            "detail": "Must be a whole number between 1 and 6",
                            "type": "https://api.example.com/errors/invalid-quantity"
                        }
                    }
                }
            }
            """)!;

        var node = schema.Root.Properties!["quantity"];
        node.ErrorInfo.Should().NotBeNull();
        node.ErrorInfo!.Value.Code.Should().Be("INVALID_QUANTITY");
        node.ErrorInfo!.Value.Title.Should().Be("Invalid quantity");
        node.ErrorInfo!.Value.Detail.Should().Be("Must be a whole number between 1 and 6");
        node.ErrorInfo!.Value.Type.Should().Be("https://api.example.com/errors/invalid-quantity");
    }

    [Test]
    public void XError_PartialFields_OnlySpecifiedFieldsPopulated()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "x-error": {
                            "code": "INVALID_NAME"
                        }
                    }
                }
            }
            """)!;

        var node = schema.Root.Properties!["name"];
        node.ErrorInfo.Should().NotBeNull();
        node.ErrorInfo!.Value.Code.Should().Be("INVALID_NAME");
        node.ErrorInfo!.Value.Title.Should().BeNull();
        node.ErrorInfo!.Value.Detail.Should().BeNull();
        node.ErrorInfo!.Value.Type.Should().BeNull();
    }

    [Test]
    public void XError_NotPresent_ErrorInfoIsNull()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string" }
                }
            }
            """)!;

        schema.Root.Properties!["name"].ErrorInfo.Should().BeNull();
    }

    [Test]
    public void XError_EmptyObject_ErrorInfoIsNull()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "x-error": {}
                    }
                }
            }
            """)!;

        schema.Root.Properties!["name"].ErrorInfo.Should().BeNull();
    }

    [Test]
    public void XError_NonObjectValue_IgnoredGracefully()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "x-error": "not an object"
                    }
                }
            }
            """)!;

        schema.Root.Properties!["name"].ErrorInfo.Should().BeNull();
    }

    // ── Enrichment ───────────────────────────────────────────────────────

    [Test]
    public void XError_TypeMismatch_ErrorIsEnriched()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "x-error": {
                            "code": "INVALID_QUANTITY",
                            "detail": "Quantity must be a whole number"
                        }
                    }
                }
            }
            """,
            """{"quantity": "not a number"}""")!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];
        error.Path.Should().Be("/quantity");
        error.ErrorInfo.Should().NotBeNull();
        error.ErrorInfo!.Value.Code.Should().Be("INVALID_QUANTITY");
        error.Message.Should().Be("Quantity must be a whole number");
    }

    [Test]
    public void XError_MaximumExceeded_ErrorIsEnriched()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "maximum": 6,
                        "x-error": {
                            "code": "INVALID_QUANTITY",
                            "title": "Invalid quantity",
                            "detail": "Must be between 1 and 6"
                        }
                    }
                }
            }
            """,
            """{"quantity": 10}""")!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];
        error.Path.Should().Be("/quantity");
        error.ErrorInfo.Should().NotBeNull();
        error.ErrorInfo!.Value.Code.Should().Be("INVALID_QUANTITY");
        error.ErrorInfo!.Value.Title.Should().Be("Invalid quantity");
        error.Message.Should().Be("Must be between 1 and 6");
    }

    [Test]
    public void XError_WithoutXError_ErrorInfoIsNull()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "maximum": 6
                    }
                }
            }
            """,
            """{"quantity": 10}""")!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];
        error.Path.Should().Be("/quantity");
        error.ErrorInfo.Should().BeNull();
    }

    [Test]
    public void XError_ValidData_NoErrors()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "maximum": 6,
                        "x-error": {
                            "code": "INVALID_QUANTITY",
                            "detail": "Must be between 1 and 6"
                        }
                    }
                }
            }
            """,
            """{"quantity": 3}""")!;

        result.Value.IsValid.Should().BeTrue();
    }

    [Test]
    public void XError_MultipleProperties_EachEnrichedIndependently()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "minLength": 2,
                        "x-error": {
                            "code": "INVALID_NAME",
                            "detail": "Name must be at least 2 characters"
                        }
                    },
                    "age": {
                        "type": "integer",
                        "minimum": 0,
                        "x-error": {
                            "code": "INVALID_AGE",
                            "detail": "Age must be a non-negative number"
                        }
                    }
                }
            }
            """,
            """{"name": "x", "age": -1}""")!;

        result.Value.IsValid.Should().BeFalse();
        result.Value.Errors.Count.Should().Be(2);

        var nameError = result.Value.Errors[0];
        var ageError = result.Value.Errors[1];

        // Order may vary, so check by path
        var errors = new[] { nameError, ageError }.ToDictionary(e => e.Path);

        errors["/name"].ErrorInfo!.Value.Code.Should().Be("INVALID_NAME");
        errors["/name"].Message.Should().Be("Name must be at least 2 characters");

        errors["/age"].ErrorInfo!.Value.Code.Should().Be("INVALID_AGE");
        errors["/age"].Message.Should().Be("Age must be a non-negative number");
    }

    [Test]
    public void XError_DetailOverridesMessage_CodePreserved()
    {
        using var result = Parse("""
            {
                "type": "object",
                "properties": {
                    "email": {
                        "type": "string",
                        "x-error": {
                            "detail": "Please provide a valid email address"
                        }
                    }
                }
            }
            """,
            """{"email": 42}""")!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];

        // detail overrides message
        error.Message.Should().Be("Please provide a valid email address");
        // original validation code preserved
        error.Code.Should().Be(ValidationErrorCode.TypeMismatch);
        // ErrorInfo still present but only has detail
        error.ErrorInfo!.Value.Code.Should().BeNull();
        error.ErrorInfo!.Value.Detail.Should().Be("Please provide a valid email address");
    }

    [Test]
    public void XError_SpanOverload_AlsoEnriches()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "quantity": {
                        "type": "integer",
                        "x-error": {
                            "code": "INVALID_QUANTITY",
                            "detail": "Must be a number"
                        }
                    }
                }
            }
            """)!;

        var data = Encoding.UTF8.GetBytes("""{"quantity": "nope"}""");
        using var result = schema.Parse(data.AsSpan())!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];
        error.ErrorInfo.Should().NotBeNull();
        error.ErrorInfo!.Value.Code.Should().Be("INVALID_QUANTITY");
        error.Message.Should().Be("Must be a number");
    }

    [Test]
    public void XError_OnRootSchema_EnrichesRootErrors()
    {
        using var result = Parse("""
            {
                "type": "object",
                "x-error": {
                    "code": "INVALID_PAYLOAD",
                    "detail": "Request body must be a JSON object"
                }
            }
            """,
            """42""")!;

        result.Value.IsValid.Should().BeFalse();
        var error = result.Value.Errors[0];
        error.Path.Should().Be("");
        error.ErrorInfo.Should().NotBeNull();
        error.ErrorInfo!.Value.Code.Should().Be("INVALID_PAYLOAD");
        error.Message.Should().Be("Request body must be a JSON object");
    }

    [Test]
    public void XError_UnknownFieldsInXError_IgnoredGracefully()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "x-error": {
                            "code": "INVALID",
                            "foo": "bar",
                            "nested": { "deep": true }
                        }
                    }
                }
            }
            """)!;

        var node = schema.Root.Properties!["name"];
        node.ErrorInfo.Should().NotBeNull();
        node.ErrorInfo!.Value.Code.Should().Be("INVALID");
    }
}
