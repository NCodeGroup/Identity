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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides common functionality for OpenID management API endpoint handlers, including resource-based
/// authorization and standard <c>GET</c> response processing.
/// </summary>
[PublicAPI]
public abstract class BaseApiEndpointHandler
{
    /// <summary>
    /// Gets the <see cref="IAuthorizationService"/> used to perform resource-based authorization.
    /// </summary>
    protected abstract IAuthorizationService AuthorizationService { get; }

    internal virtual IReadOnlyCollection<SecretResource> ToSecretsResource(
        IReadOnlyCollection<PersistedSecret> secrets
    )
    {
        return secrets.Select(ToSecretResource).ToList();
    }

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

        if (user.Identity?.IsAuthenticated ?? false)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Unauthorized();
    }
}
