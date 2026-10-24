#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.Persistence;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides the common, backing-store-agnostic functionality for OpenID management API endpoint handlers — resource-based
/// authorization and the standard <c>GET</c>/collection response processing. It is public so capability packages that
/// plug into the management slice (for example, local accounts) can author their own endpoints without taking a
/// dependency on the management implementation package.
/// </summary>
[PublicAPI]
public abstract class ManagementApiEndpointHandler
{
    /// <summary>
    /// The default page size applied to a collection <c>GET</c> when the caller does not specify a limit.
    /// </summary>
    protected const int DefaultPageSize = 50;

    /// <summary>
    /// The maximum page size a collection <c>GET</c> will return regardless of the caller's requested limit.
    /// </summary>
    protected const int MaxPageSize = 100;

    /// <summary>
    /// Gets the <see cref="IAuthorizationService"/> used to perform resource-based authorization.
    /// </summary>
    protected abstract IAuthorizationService AuthorizationService { get; }

    /// <summary>
    /// Gets the <see cref="ICryptoService"/> used to generate opaque resource identifiers.
    /// </summary>
    protected abstract ICryptoService CryptoService { get; }

    // A *Resource projection is the vetted non-sensitive view of a persisted entity; serializing one
    // yields an audit snapshot that cannot leak secret material.
    /// <summary>
    /// Serializes a non-sensitive <c>*Resource</c> projection into a detached <see cref="JsonElement"/> suitable for an
    /// audit snapshot.
    /// </summary>
    /// <param name="resource">The non-sensitive resource projection to serialize.</param>
    /// <typeparam name="T">The type of the resource projection.</typeparam>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    protected static JsonElement ToResourceValues<T>(T resource) =>
        JsonSerializer.SerializeToElement(resource, JsonSerializerOptions.Web);

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
    protected internal virtual ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        TValue? valueOrNull,
        IAuthorizationRequirement authorizationRequirement,
        Func<TValue, TResponse> mapper
    ) =>
        ProcessGetAsync(
            httpContext,
            valueOrNull,
            authorizationResource: null,
            authorizationRequirement,
            mapper
        );

    /// <summary>
    /// Processes an HTTP <c>GET</c> request for the specified value, authorizing against a separately-supplied
    /// resource (such as the owning resource node) while mapping and returning the value. Emits an <c>ETag</c> and
    /// honors <c>If-None-Match</c> when the value supports a concurrency token. When <paramref name="mapper"/> returns
    /// <c>null</c>, the mapped sub-resource is treated as absent and the request returns <c>404 Not Found</c> (for
    /// example, a symmetric secret has no public key to project).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="valueOrNull">The value to map and return, or <c>null</c> when not found.</param>
    /// <param name="authorizationResource">The resource to authorize against, or <c>null</c> to authorize against the
    /// value itself.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate.</param>
    /// <param name="mapper">A function that maps the value to the response representation, or returns <c>null</c> to
    /// report the mapped sub-resource as absent.</param>
    /// <typeparam name="TValue">The type of the value being processed.</typeparam>
    /// <typeparam name="TResponse">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        TValue? valueOrNull,
        object? authorizationResource,
        IAuthorizationRequirement authorizationRequirement,
        Func<TValue, TResponse> mapper
    ) =>
        ProcessGetAsync(
            httpContext,
            valueOrNull,
            authorizationResource,
            authorizationRequirement,
            (value, _) => ValueTask.FromResult(mapper(value)),
            CancellationToken.None
        );

    /// <summary>
    /// Processes an HTTP <c>GET</c> request for the specified value, authorizing against a separately-supplied
    /// resource (such as the owning resource node) while mapping and returning the value. Emits an <c>ETag</c> and
    /// honors <c>If-None-Match</c> when the value supports a concurrency token. When <paramref name="mapperAsync"/>
    /// returns <c>null</c>, the mapped sub-resource is treated as absent and the request returns <c>404 Not Found</c>
    /// (for example, a symmetric secret has no public key to project). The mapper runs only after authorization
    /// succeeds, so it may do work an unauthorized caller must not cause (such as loading key material).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="valueOrNull">The value to map and return, or <c>null</c> when not found.</param>
    /// <param name="authorizationResource">The resource to authorize against, or <c>null</c> to authorize against the
    /// value itself.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate.</param>
    /// <param name="mapperAsync">A function that asynchronously maps the value to the response representation, or
    /// returns <c>null</c> to report the mapped sub-resource as absent. It runs only after authorization succeeds.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TValue">The type of the value being processed.</typeparam>
    /// <typeparam name="TResponse">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        TValue? valueOrNull,
        object? authorizationResource,
        IAuthorizationRequirement authorizationRequirement,
        Func<TValue, CancellationToken, ValueTask<TResponse>> mapperAsync,
        CancellationToken cancellationToken
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
            authorizationResource ?? valueOrNull,
            authorizationRequirement
        );

        if (authorizationResult.Succeeded)
        {
            // Project first so an absent sub-resource short-circuits to 404 before emitting an ETag or a 304.
            var response = await mapperAsync(valueOrNull, cancellationToken);
            if (response is null)
            {
                // A null projection signals an absent sub-resource (e.g. a symmetric secret has no public key).
                return TypedResults.NotFound();
            }

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

            return TypedResults.Json(response);
        }

        return AuthorizationFailed(httpContext);
    }

    /// <summary>
    /// Clamps a caller-supplied page limit to the supported range, applying the default when unspecified.
    /// </summary>
    /// <param name="limit">The requested page limit, or <c>null</c>.</param>
    /// <returns>The effective page limit.</returns>
    protected internal virtual int NormalizeLimit(int? limit) =>
        Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

    /// <summary>
    /// Processes an HTTP collection <c>GET</c> request: authorizes the caller to enumerate the family, then fetches,
    /// maps, and returns a single page. Authorization runs before the page is fetched so an unauthorized request never
    /// touches the store.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="authorizationResource">The resource evaluated by <see cref="Operations.List"/> authorization — a
    /// tenant-scoped marker for a tenant-bound family, or <c>null</c> for a central-admin family.</param>
    /// <param name="fetchPageAsync">A function that fetches the page once authorization has succeeded.</param>
    /// <param name="mapper">A function that maps a persisted item to its response representation.</param>
    /// <typeparam name="TItem">The type of the persisted item.</typeparam>
    /// <typeparam name="TResource">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessListAsync<TItem, TResource>(
        HttpContext httpContext,
        object? authorizationResource,
        Func<ValueTask<PagedResult<TItem>>> fetchPageAsync,
        Func<TItem, TResource> mapper
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            authorizationResource,
            Operations.List
        );

        if (!authorizationResult.Succeeded)
        {
            return AuthorizationFailed(httpContext);
        }

        var page = await fetchPageAsync();

        var items = page.Items.Select(mapper).ToList();
        return TypedResults.Json(
            new CollectionResource<TResource> { Items = items, ContinuationToken = page.NextCursor }
        );
    }
}
