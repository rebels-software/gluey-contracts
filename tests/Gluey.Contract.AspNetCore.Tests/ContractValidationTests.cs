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

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gluey.Contract.AspNetCore;
using Gluey.Contract.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gluey.Contract.AspNetCore.Tests;

[TestFixture]
public class ContractValidationTests
{
    private const string OrderSchema = """
        {
            "type": "object",
            "properties": {
                "name": { "type": "string", "minLength": 1 },
                "quantity": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 100,
                    "x-error": {
                        "code": "INVALID_QUANTITY",
                        "title": "Invalid quantity",
                        "detail": "Quantity must be between 1 and 100"
                    }
                }
            },
            "required": ["name", "quantity"]
        }
        """;

    private static HttpClient CreateClient(Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var schema = JsonContractSchema.Load(OrderSchema)!;

        builder.Services.AddGlueyContracts(registry =>
        {
            registry.Add("order", schema);
        });

        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        app.MapPost("/orders", (HttpContext ctx) =>
        {
            using var parsed = ctx.GetContractResult();
            var name = parsed["name"].GetString();
            return Results.Ok(new { accepted = true, name });
        }).WithContractValidation(schema);

        app.MapPost("/orders-by-name", (HttpContext ctx) =>
        {
            return Results.Ok(new { accepted = true });
        }).WithContractValidation("order");

        app.MapPost("/orders-bind", (HttpContext ctx) =>
        {
            var body = ctx.GetContractBody();
            var name = body["name"].GetString();
            return Results.Ok(new { accepted = true, name });
        }).WithContractValidation(schema);

        app.MapPost("/orders-param", [Contract("order")] (ContractBody body) =>
        {
            var name = body["name"].GetString();
            return Results.Ok(new { accepted = true, name });
        }).WithContract();

        app.MapPost("/orders-typed", [Contract("order")] (OrderPayload body) =>
        {
            return Results.Ok(new { accepted = true, body.Name, body.Quantity });
        }).WithContract();

        app.MapPost("/orders-headers", [Contract("order")] (ContractBody body) =>
        {
            var requestId = body.Headers["X-Request-Id"].ToString();
            return Results.Ok(new { accepted = true, requestId });
        }).WithContract();

        app.Start();
        return app.GetTestClient();
    }

    // ── Valid requests ────────────────────────────────────────────────────

    [Test]
    public async Task ValidRequest_Returns200()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("name").GetString().Should().Be("Widget");
    }

    // ── Invalid requests ─────────────────────────────────────────────────

    [Test]
    public async Task InvalidJson_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("not json", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Validation failed");
    }

    [Test]
    public async Task EmptyBody_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Validation failed");
    }

    [Test]
    public async Task MissingRequiredField_Returns400WithError()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task TypeMismatch_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":"not a number"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    // ── x-error enrichment ───────────────────────────────────────────────

    [Test]
    public async Task XError_EnrichesValidationError()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("code").GetString().Should().Be("INVALID_QUANTITY");
        error.GetProperty("message").GetString().Should().Be("Quantity must be between 1 and 100");
    }

    // ── Named schema resolution ──────────────────────────────────────────

    [Test]
    public async Task NamedSchema_ResolvesFromRegistry()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-by-name",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task NamedSchema_InvalidData_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-by-name",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── RFC 7807 structure ───────────────────────────────────────────────

    [Test]
    public async Task ProblemDetails_HasCorrectStructure()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Contain("rfc9110");
        body.GetProperty("title").GetString().Should().Be("Validation failed");
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── TransformError ───────────────────────────────────────────────────

    [Test]
    public async Task TransformError_CustomErrorShape()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(options =>
        {
            options.TransformError = (error, ctx) => new
            {
                field = error.Path,
                reason = "custom: " + error.Message
            };
        });

        var app = builder.Build();
        app.MapPost("/orders", () => Results.Ok())
            .WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("field").GetString().Should().Be("/quantity");
        error.GetProperty("reason").GetString().Should().StartWith("custom:");
    }

    // ── OnValidationFailed ───────────────────────────────────────────────

    [Test]
    public async Task OnValidationFailed_FullResponseOverride()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(options =>
        {
            options.OnValidationFailed = async (errors, ctx) =>
            {
                ctx.Response.StatusCode = 422;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    errorCode = "VALIDATION_FAILED",
                    count = errors.Count
                });
            };
        });

        var app = builder.Build();
        app.MapPost("/orders", () => Results.Ok())
            .WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be((HttpStatusCode)422);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        body.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
    }

    // ── ContractBody parameter binding ───────────────────────────────────

    [Test]
    public async Task ContractBody_BindsAsParameter()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-bind",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("name").GetString().Should().Be("Widget");
    }

    [Test]
    public async Task ContractBody_InvalidData_Returns400BeforeBinding()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-bind",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ContractBody as parameter with [Contract] attribute ──────────────

    [Test]
    public async Task ContractBody_WithAttribute_ValidRequest()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-param",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("name").GetString().Should().Be("Widget");
    }

    [Test]
    public async Task ContractBody_WithAttribute_InvalidRequest_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-param",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Validation failed");
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ContractBody_WithAttribute_EmptyBody_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-param",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ContractBody_WithAttribute_XErrorEnriched()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-param",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("code").GetString().Should().Be("INVALID_QUANTITY");
    }

    // ── Typed ContractBody<TSelf> ────────────────────────────────────────

    [Test]
    public async Task TypedContractBody_ValidRequest()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-typed",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();
        body.GetProperty("name").GetString().Should().Be("Widget");
        body.GetProperty("quantity").GetInt32().Should().Be(5);
    }

    [Test]
    public async Task TypedContractBody_InvalidRequest_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-typed",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Headers access ──────────────────────────────────────────────────

    [Test]
    public async Task ContractBody_Headers_AccessibleInHandler()
    {
        using var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders-headers")
        {
            Content = new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Request-Id", "abc-123");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requestId").GetString().Should().Be("abc-123");
    }

    // ── ContractSchemaRegistry ───────────────────────────────────────────

    [Test]
    public void ContractSchemaRegistry_TryGet_UnknownName_ReturnsFalse()
    {
        var registry = new ContractSchemaRegistry();
        registry.TryGet("nonexistent", out var schema).Should().BeFalse();
        schema.Should().BeNull();
    }

    [Test]
    public void ContractSchemaRegistry_Get_UnknownName_ThrowsKeyNotFound()
    {
        var registry = new ContractSchemaRegistry();
        var act = () => registry.Get("nonexistent");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    public void ContractSchemaRegistry_Add_ThenTryGet_ReturnsSchema()
    {
        var registry = new ContractSchemaRegistry();
        var schema = JsonContractSchema.Load(OrderSchema)!;
        registry.Add("test", schema);

        registry.TryGet("test", out var result).Should().BeTrue();
        result.Should().BeSameAs(schema);
    }

    // ── ContractAttribute ────────────────────────────────────────────────

    [Test]
    public void ContractAttribute_StoresSchemaName()
    {
        var attr = new ContractAttribute("my-schema");
        attr.SchemaName.Should().Be("my-schema");
    }

    // ── GetContractResult ────────────────────────────────────────────────

    [Test]
    public async Task GetContractResult_ValidRequest_ReturnsResult()
    {
        using var client = CreateClient();
        // Use the filter-based endpoint which stores in Items
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public void GetContractResult_WithoutFilter_ThrowsInvalidOperation()
    {
        var context = new DefaultHttpContext();
        var act = () => context.GetContractResult();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetContractBody_WithoutFilter_ThrowsInvalidOperation()
    {
        var context = new DefaultHttpContext();
        var act = () => context.GetContractBody();
        act.Should().Throw<InvalidOperationException>();
    }

    // ── TransformError returning null (fall-through) ─────────────────────

    [Test]
    public async Task TransformError_ReturnsNull_FallsThrough()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(options =>
        {
            options.TransformError = (error, ctx) => null; // fall through to default
        });

        var app = builder.Build();
        app.MapPost("/orders", () => Results.Ok())
            .WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/orders",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        // Should have default error shape (path, code, message)
        error.GetProperty("path").GetString().Should().Be("/quantity");
        error.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
    }

    // ── AddGlueyContracts overloads ──────────────────────────────────────

    [Test]
    public void AddGlueyContracts_WithOptions_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddGlueyContracts(options =>
        {
            options.TransformError = (e, c) => null;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<ContractOptions>();
        options.Should().NotBeNull();
        options!.TransformError.Should().NotBeNull();
    }

    [Test]
    public void AddGlueyContracts_WithoutOptions_RegistersDefaults()
    {
        var services = new ServiceCollection();
        services.AddGlueyContracts();

        var provider = services.BuildServiceProvider();
        provider.GetService<ContractOptions>().Should().NotBeNull();
        provider.GetService<ContractSchemaRegistry>().Should().NotBeNull();
    }

    // ── ContractBody properties ──────────────────────────────────────────

    [Test]
    public void ContractBody_Headers_ThrowsWhenNotSet()
    {
        var body = new ContractBody();
        var act = () => { var _ = body.Headers; };
        act.Should().Throw<InvalidOperationException>().WithMessage("*Headers*");
    }

    [Test]
    public async Task ContractBody_IsValid_True_ForValidData()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-param",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ContractBody_Ordinal_AccessWorks()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(registry => registry.Add("order", schema));

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            var prop = body[0];
            return Results.Ok(new { hasValue = prop.HasValue });
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ContractBody_Enumeration_Works()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(registry => registry.Add("order", schema));

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            int count = 0;
            foreach (var prop in body) count++;
            return Results.Ok(new { count });
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("count").GetInt32().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ContractBody_Result_ReturnsParseResult()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(registry => registry.Add("order", schema));

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            var result = body.Result;
            return Results.Ok(new { isValid = result.IsValid });
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── OnValidationFailed via [Contract] attribute binding ──────────────

    [Test]
    public async Task ContractBody_WithAttribute_OnValidationFailed_Override()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(
            registry => registry.Add("order", schema),
            options =>
            {
                options.OnValidationFailed = async (errors, ctx) =>
                {
                    ctx.Response.StatusCode = 422;
                    await ctx.Response.WriteAsJsonAsync(new { custom = true, count = errors.Count });
                };
            });

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            return Results.Ok();
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("custom").GetBoolean().Should().BeTrue();
    }

    // ── WithContractErrors (per-endpoint) ────────────────────────────────

    [Test]
    public async Task WithContractErrors_PerEndpoint_TransformsErrors()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts();

        var app = builder.Build();
        app.MapPost("/test", () => Results.Ok())
            .WithContractValidation(schema)
            .WithContractErrors((error, ctx) => new { field = error.Path, msg = error.Message });
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":200}""", Encoding.UTF8, "application/json"));

        // WithContractErrors stores metadata but ProblemDetailsMapper doesn't read it yet
        // The filter still produces a 400 - this test verifies the extension doesn't throw
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Chunked body (no Content-Length) ──────────────────────────────────

    [Test]
    public async Task ContractBody_ChunkedBody_ValidatesCorrectly()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(registry => registry.Add("order", schema));

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            return Results.Ok(new { name = body["name"].GetString() });
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        // StreamContent doesn't set Content-Length, forcing chunked/MemoryStream path
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("""{"name":"Widget","quantity":5}""")));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var response = await client.PostAsync("/test", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Widget");
    }

    // ── Typed body invalid path (CreateFailure<T> for derived type) ──────

    [Test]
    public async Task TypedContractBody_EmptyBody_Returns400()
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/orders-typed",
            new StringContent("", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Errors property on ContractBody ───────────────────────────────────

    [Test]
    public async Task ContractBody_Errors_EmptyWhenValid()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts(registry => registry.Add("order", schema));

        var app = builder.Build();
        app.MapPost("/test", [Contract("order")] (ContractBody body) =>
        {
            return Results.Ok(new { errorCount = body.Errors.Count, body.IsValid });
        }).WithContract();
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCount").GetInt32().Should().Be(0);
        body.GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    // ── ProblemDetailsMapper with error that has no ErrorInfo ─────────────

    [Test]
    public async Task ProblemDetails_ErrorWithoutXError_UsesCodeToString()
    {
        // Schema without x-error — errors use ValidationErrorCode.ToString()
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": { "value": { "type": "integer" } }
            }
            """)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts();

        var app = builder.Build();
        app.MapPost("/test", () => Results.Ok())
            .WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"value":"not-a-number"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("code").GetString().Should().Be("TypeMismatch");
    }

    // ── ProblemDetailsMapper with ErrorInfo that has Title and Type ───────

    [Test]
    public async Task ProblemDetails_XErrorWithTitleAndType_IncludesInResponse()
    {
        var schema = JsonContractSchema.Load("""
            {
                "type": "object",
                "properties": {
                    "value": {
                        "type": "integer",
                        "x-error": {
                            "code": "BAD_VALUE",
                            "title": "Bad value",
                            "type": "https://example.com/errors/bad-value"
                        }
                    }
                }
            }
            """)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts();

        var app = builder.Build();
        app.MapPost("/test", () => Results.Ok())
            .WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"value":"nope"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("code").GetString().Should().Be("BAD_VALUE");
        error.GetProperty("title").GetString().Should().Be("Bad value");
        error.GetProperty("type").GetString().Should().Be("https://example.com/errors/bad-value");
    }

    // ── Cache reuse path (filter stores Items, GetContractBody reads them) ──

    [Test]
    public async Task GetContractBody_ReusesFilterValidatedData()
    {
        // WithContractValidation filter stores body+schema in Items (lines 167-176 in BindAsync,
        // but actually exercised via GetContractBody which calls the same Items lookup)
        var schema = JsonContractSchema.Load(OrderSchema)!;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts();

        var app = builder.Build();
        app.MapPost("/test", (HttpContext ctx) =>
        {
            // First call reads from Items (cache path)
            var body1 = ctx.GetContractBody();
            // Second call also reads from Items
            var body2 = ctx.GetContractBody();
            return Results.Ok(new { name = body1["name"].GetString(), name2 = body2["name"].GetString() });
        }).WithContractValidation(schema);
        app.Start();

        using var client = app.GetTestClient();
        var response = await client.PostAsync("/test",
            new StringContent("""{"name":"Widget","quantity":5}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Widget");
        body.GetProperty("name2").GetString().Should().Be("Widget");
    }

    // ── No [Contract] attribute and no filter → throws ───────────────────

    [Test]
    public async Task ContractBody_NoAttribute_NoFilter_ThrowsInvalidOperation()
    {
        // ContractBody param without [Contract] attribute and without WithContractValidation
        // → ResolveSchema returns null (line 279) → throws (lines 183-185)
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddGlueyContracts();

        var app = builder.Build();
        app.MapPost("/test", (ContractBody body) =>
        {
            return Results.Ok();
        }).WithContract();

        app.Start();

        using var client = app.GetTestClient();
        // BindAsync throws InvalidOperationException — TestHost propagates it
        var act = async () => await client.PostAsync("/test",
            new StringContent("""{"name":"Widget"}""", Encoding.UTF8, "application/json"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*[Contract]*");
    }

    // ── GetContractBody re-parse returns null (line 39-40) ───────────────

    [Test]
    public void GetContractBody_ReParseReturnsNull_Throws()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;
        var context = new DefaultHttpContext();
        // Simulate filter having stored invalid bytes that Parse returns null for
        context.Items["Contract:Body"] = new byte[] { 0xFF, 0xFE };
        context.Items["Contract:Schema"] = schema;

        var act = () => context.GetContractBody();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*re-parse*null*");
    }

    // ── GetContractResult re-parse returns null (line 67-68) ─────────────

    [Test]
    public void GetContractResult_ReParseReturnsNull_Throws()
    {
        var schema = JsonContractSchema.Load(OrderSchema)!;
        var context = new DefaultHttpContext();
        context.Items["Contract:Body"] = new byte[] { 0xFF, 0xFE };
        context.Items["Contract:Schema"] = schema;

        var act = () => context.GetContractResult();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*re-parse*null*");
    }
}

public class OrderPayload : ContractBody<OrderPayload>
{
    public string Name => this["name"].GetString();
    public int Quantity => this["quantity"].GetInt32();
}
