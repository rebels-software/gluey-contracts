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

using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Gluey.Contract.AspNetCore;

/// <summary>
/// Bindable parsed request body for ASP.NET Core minimal APIs.
/// Reads, validates, and short-circuits with RFC 7807 on failure — all during parameter binding.
/// Auto-disposed at the end of the request.
///
/// <para>Usage with <c>[Contract]</c> attribute:</para>
/// <code>
/// app.MapPost("/orders", [Contract("order")] (ContractBody body) =>
/// {
///     var name = body["name"].GetString();
///     return Results.Ok(new { name });
/// }).WithContract();
/// </code>
///
/// <para>For typed access, subclass <see cref="ContractBody{TSelf}"/>:</para>
/// <code>
/// public class OrderPayload : ContractBody&lt;OrderPayload&gt;
/// {
///     public string Name => this["name"].GetString();
///     public int Quantity => this["quantity"].GetInt32();
/// }
/// </code>
/// </summary>
public class ContractBody : IDisposable
{
    private ParseResult _result;
    private object? _validationFailure;
    private IHeaderDictionary? _headers;

    /// <summary>
    /// Parameterless constructor for derived types and the <c>new()</c> constraint.
    /// </summary>
    public ContractBody() { }

    /// <summary>
    /// Creates a new <see cref="ContractBody"/> wrapping the given <see cref="ParseResult"/>.
    /// </summary>
    internal ContractBody(ParseResult result)
    {
        _result = result;
    }

    /// <summary>
    /// Creates a <see cref="ContractBody"/> representing a validation failure.
    /// </summary>
    internal ContractBody(object validationFailure, bool _)
    {
        _validationFailure = validationFailure;
    }

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> with the given name.
    /// </summary>
    public ParsedProperty this[string name] => _result[name];

    /// <summary>
    /// Gets the <see cref="ParsedProperty"/> at the given ordinal.
    /// </summary>
    public ParsedProperty this[int ordinal] => _result[ordinal];

    /// <summary>Whether the parsed data passed all schema validations.</summary>
    public bool IsValid => _validationFailure is null && !_result.Errors.HasErrors;

    /// <summary>The collected validation errors (empty when valid).</summary>
    public ErrorCollector Errors => _result.Errors;

    /// <summary>Returns the underlying <see cref="ParseResult"/>.</summary>
    public ParseResult Result => _result;

    /// <summary>The request headers. Available for reading request metadata without needing <c>HttpContext</c>.</summary>
    public IHeaderDictionary Headers => _headers ?? throw new InvalidOperationException("Headers not available.");

    /// <summary>Sets the headers reference. Used internally during binding.</summary>
    internal void SetHeaders(IHeaderDictionary headers) => _headers = headers;

    /// <summary>
    /// Returns a struct enumerator over all parsed properties that have values.
    /// </summary>
    public ParseResult.Enumerator GetEnumerator() => _result.GetEnumerator();

    /// <summary>Disposes the underlying <see cref="ParseResult"/> and returns pooled buffers.</summary>
    public void Dispose()
    {
        _result.Dispose();
    }

    /// <summary>
    /// Gets the validation failure response, or <c>null</c> if validation passed.
    /// Used by <see cref="ContractBodyValidationFilter"/> to short-circuit.
    /// </summary>
    internal object? ValidationFailure => _validationFailure;

    /// <summary>
    /// Binds a <see cref="ContractBody"/> from the HTTP request.
    /// </summary>
    public static async ValueTask<ContractBody?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        return await ContractBodyBinder.BindAsync<ContractBody>(context, parameter);
    }
}

/// <summary>
/// Generic base class for typed contract bodies.
/// Inherit from this to get strongly-typed property access with automatic <c>BindAsync</c> support.
///
/// <para>Example:</para>
/// <code>
/// public class OrderPayload : ContractBody&lt;OrderPayload&gt;
/// {
///     public string Name => this["name"].GetString();
///     public int Quantity => this["quantity"].GetInt32();
/// }
///
/// app.MapPost("/orders", [Contract("order")] (OrderPayload body) =>
/// {
///     return Results.Ok(new { body.Name, body.Quantity });
/// }).WithContract();
/// </code>
/// </summary>
/// <typeparam name="TSelf">The derived type (CRTP pattern).</typeparam>
public class ContractBody<TSelf> : ContractBody where TSelf : ContractBody<TSelf>, new()
{
    /// <inheritdoc />
    protected ContractBody() { }

    /// <summary>
    /// Binds a <typeparamref name="TSelf"/> from the HTTP request.
    /// Called automatically by the ASP.NET Core minimal API parameter binding infrastructure.
    /// </summary>
    public static new async ValueTask<TSelf?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        return await ContractBodyBinder.BindAsync<TSelf>(context, parameter);
    }
}

/// <summary>
/// Shared binding logic for <see cref="ContractBody"/> and <see cref="ContractBody{TSelf}"/>.
/// </summary>
internal static class ContractBodyBinder
{
    internal static async ValueTask<T?> BindAsync<T>(HttpContext context, ParameterInfo parameter)
        where T : ContractBody, new()
    {
        var headers = context.Request.Headers;

        // If the filter already validated, reuse stored data
        if (context.Items.TryGetValue("Contract:Body", out var cachedBody) && cachedBody is byte[] cachedBytes
            && context.Items.TryGetValue("Contract:Schema", out var cachedSchema) && cachedSchema is IContractSchema cached)
        {
            var cachedResult = cached.Parse(cachedBytes);
            if (cachedResult is { } parsed)
            {
                var body = CreateSuccess<T>(parsed, headers);
                context.Response.RegisterForDispose(body);
                return body;
            }
        }

        // Resolve schema from [Contract] attribute
        var schema = ResolveSchema(context, parameter);
        if (schema is null)
        {
            throw new InvalidOperationException(
                "No [Contract] attribute found and no schema provided via WithContractValidation(). " +
                "Add [Contract(\"schemaName\")] to your endpoint or use .WithContractValidation(schema).");
        }

        // Read the body
        var bytes = await ReadBodyAsync(context.Request);

        // Parse and validate
        var result = schema.Parse(bytes);

        if (result is null)
        {
            var failure = CreateFailure<T>(headers, new ContractProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Errors = [new ContractValidationError
                {
                    Path = "",
                    Code = "InvalidData",
                    Message = "Request body is empty or structurally invalid."
                }]
            });
            context.Response.RegisterForDispose(failure);
            return failure;
        }

        if (!result.Value.IsValid)
        {
            var options = context.RequestServices.GetService<ContractOptions>();

            if (options?.OnValidationFailed is { } handler)
            {
                var failure = CreateFailure<T>(headers, new DeferredValidationFailure(handler, result.Value.Errors));
                context.Response.RegisterForDispose(failure);
                return failure;
            }

            var problemDetails = ProblemDetailsMapper.Build(result.Value.Errors, context, options);
            result.Value.Dispose();
            var failureBody = CreateFailure<T>(headers, problemDetails);
            context.Response.RegisterForDispose(failureBody);
            return failureBody;
        }

        var contractBody = CreateSuccess<T>(result.Value, headers);
        context.Response.RegisterForDispose(contractBody);
        return contractBody;
    }

    private static readonly FieldInfo ResultField = typeof(ContractBody).GetField("_result", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo FailureField = typeof(ContractBody).GetField("_validationFailure", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo HeadersField = typeof(ContractBody).GetField("_headers", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static T CreateSuccess<T>(ParseResult result, IHeaderDictionary headers) where T : ContractBody, new()
    {
        T instance;
        if (typeof(T) == typeof(ContractBody))
            instance = (T)(object)new ContractBody(result);
        else
        {
            instance = new T();
            ResultField.SetValue(instance, result);
        }
        HeadersField.SetValue(instance, headers);
        return instance;
    }

    private static T CreateFailure<T>(IHeaderDictionary headers, object failure) where T : ContractBody, new()
    {
        T instance;
        if (typeof(T) == typeof(ContractBody))
            instance = (T)(object)new ContractBody(failure, false);
        else
        {
            instance = new T();
            FailureField.SetValue(instance, failure);
        }
        HeadersField.SetValue(instance, headers);
        return instance;
    }

    private static IContractSchema? ResolveSchema(HttpContext context, ParameterInfo parameter)
    {
        var contractAttr = parameter.Member.GetCustomAttribute<ContractAttribute>()
            ?? parameter.Member.DeclaringType?.GetCustomAttribute<ContractAttribute>();

        if (contractAttr is not null)
        {
            var registry = context.RequestServices.GetService<ContractSchemaRegistry>();
            if (registry is not null && registry.TryGet(contractAttr.SchemaName, out var schema))
                return schema;
        }

        return null;
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is > 0)
        {
            var body = new byte[(int)request.ContentLength.Value];
            var totalRead = 0;
            while (totalRead < body.Length)
            {
                var read = await request.Body.ReadAsync(body.AsMemory(totalRead));
                if (read == 0) break;
                totalRead += read;
            }
            return body;
        }

        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}

/// <summary>
/// Deferred validation failure — stores the handler and errors for the filter to execute.
/// </summary>
internal sealed class DeferredValidationFailure
{
    internal Func<ErrorCollector, HttpContext, Task> Handler { get; }
    internal ErrorCollector Errors { get; }

    internal DeferredValidationFailure(Func<ErrorCollector, HttpContext, Task> handler, ErrorCollector errors)
    {
        Handler = handler;
        Errors = errors;
    }
}
