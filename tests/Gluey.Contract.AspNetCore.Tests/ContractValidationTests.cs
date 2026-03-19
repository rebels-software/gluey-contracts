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
}

public class OrderPayload : ContractBody<OrderPayload>
{
    public string Name => this["name"].GetString();
    public int Quantity => this["quantity"].GetInt32();
}
