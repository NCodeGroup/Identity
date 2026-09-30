#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides common functionality for OpenID management API endpoint handlers, including resource-based
/// authorization and standard <c>GET</c> response processing.
/// </summary>
internal abstract class BaseApiEndpointHandler
{
    /// <summary>
    /// Gets the <see cref="IAuthorizationService"/> used to perform resource-based authorization.
    /// </summary>
    protected abstract IAuthorizationService AuthorizationService { get; }

    /// <summary>
    /// Gets the <see cref="ICryptoService"/> used to generate opaque resource identifiers.
    /// </summary>
    protected abstract ICryptoService CryptoService { get; }

    /// <summary>
    /// Serializes the specified <see cref="JsonObject"/> into a detached <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to serialize.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    internal virtual JsonElement SerializeToElement(JsonObject jsonObject)
    {
        return JsonSerializer.SerializeToElement(jsonObject);
    }

    /// <summary>
    /// Converts the specified <see cref="JsonElement"/> into a mutable <see cref="JsonObject"/>, returning an empty
    /// object when the element is <c>null</c> or not a JSON object.
    /// </summary>
    /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
    /// <returns>The resulting <see cref="JsonObject"/>.</returns>
    internal virtual JsonObject ToJsonObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Object:
                return JsonObject.Create(element) ?? new JsonObject();

            default:
                return new JsonObject();
        }
    }

    /// <summary>
    /// Returns the appropriate <see cref="IResult"/> for a failed authorization: <c>403 Forbidden</c> when the
    /// user is authenticated (but lacks permission) and <c>401 Unauthorized</c> when the user is anonymous.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The <see cref="IResult"/> representing the authorization failure.</returns>
    protected internal virtual IResult AuthorizationFailed(HttpContext httpContext)
    {
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
        return isAuthenticated ? TypedResults.Forbid() : TypedResults.Unauthorized();
    }

    /// <summary>
    /// Maps a core-validation <see cref="ManagementError"/> to its <see cref="IResult"/> response, using the
    /// error's fixed status and safe detail message.
    /// </summary>
    /// <param name="error">The <see cref="ManagementError"/> to map.</param>
    /// <returns>The <see cref="IResult"/> representing the failure.</returns>
    protected internal virtual IResult ToErrorResult(ManagementError error)
    {
        return TypedResults.Problem(detail: error.Detail, statusCode: error.StatusCode);
    }

    /// <summary>
    /// Maps a collection of <see cref="PersistedSecret"/> instances to their <see cref="SecretResource"/> representations.
    /// </summary>
    /// <param name="secrets">The collection of <see cref="PersistedSecret"/> instances to map.</param>
    /// <returns>The mapped read-only collection of <see cref="SecretResource"/> instances.</returns>
    internal virtual IReadOnlyCollection<SecretResource> ToSecretsResource(
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        return secrets.Select(ToSecretResource).ToList();
    }

    /// <summary>
    /// Maps a single <see cref="PersistedSecret"/> instance to its <see cref="SecretResource"/> representation.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> instance to map.</param>
    /// <returns>The mapped <see cref="SecretResource"/>.</returns>
    internal virtual SecretResource ToSecretResource(PersistedSecret secret)
    {
        return new SecretResource
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
        };
    }

    /// <summary>
    /// Processes an HTTP <c>GET</c> request for the specified value, returning the value as-is when found and authorized.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="valueOrNull">The value to return, or <c>null</c> when not found.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate against the value.</param>
    /// <typeparam name="TValue">The type of the value to return.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue>(
        HttpContext httpContext,
        TValue? valueOrNull,
        IAuthorizationRequirement authorizationRequirement
    )
    {
        return await ProcessGetAsync(
            httpContext,
            valueOrNull,
            authorizationRequirement,
            value => value
        );
    }

    /// <summary>
    /// Processes an HTTP <c>GET</c> request for the specified value, mapping it to a response when found and authorized.
    /// Emits an <c>ETag</c> and honors <c>If-None-Match</c> when the value supports a concurrency token.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="valueOrNull">The value to map and return, or <c>null</c> when not found.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate against the value.</param>
    /// <param name="mapper">A function that maps the value to the response representation.</param>
    /// <typeparam name="TValue">The type of the value being processed.</typeparam>
    /// <typeparam name="TResponse">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        TValue? valueOrNull,
        IAuthorizationRequirement authorizationRequirement,
        Func<TValue, TResponse> mapper
    )
    {
        if (valueOrNull is null)
        {
            return TypedResults.NotFound();
        }

        // https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-9.0
        var user = httpContext.User;
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            user,
            valueOrNull,
            authorizationRequirement
        );

        if (authorizationResult.Succeeded)
        {
            if (valueOrNull is ISupportConcurrencyToken supportConcurrencyToken)
            {
                var concurrencyToken = supportConcurrencyToken.ConcurrencyToken;
                httpContext.Response.Headers.ETag = concurrencyToken;

                var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch;
                if (string.Equals(ifNoneMatch, concurrencyToken, StringComparison.Ordinal))
                {
                    return TypedResults.StatusCode(StatusCodes.Status304NotModified);
                }
            }

            var response = mapper(valueOrNull);
            return TypedResults.Json(response);
        }

        return AuthorizationFailed(httpContext);
    }
}
