// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.Persistence;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides the common, backing-store-agnostic functionality for OpenID management API endpoint handlers — resource-based
/// authorization and the standard <c>GET</c>/collection response processing. A <c>GET</c> authorizes against a
/// node and only then reads the value, so an unauthorized caller never touches the store. It is public so
/// capability packages that
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
    /// Returns the problem-details <see cref="IResult"/> for a failed authorization: <c>403 Forbidden</c> when the
    /// user is authenticated (but lacks permission) and <c>401 Unauthorized</c> when the user is anonymous. The
    /// management API publishes the bearer principal itself through <c>ManagementAuthenticationEndpointFilter</c>
    /// and never through an authentication scheme, so the refusal never runs a scheme's forbid or challenge, which
    /// could redirect to an interactive page.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The <see cref="IResult"/> representing the authorization failure.</returns>
    protected internal virtual IResult AuthorizationFailed(HttpContext httpContext)
    {
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
        return isAuthenticated
            ? TypedResults.Problem(
                detail: "The caller is not authorized for this operation.",
                statusCode: StatusCodes.Status403Forbidden
            )
            : ManagementProblems.AuthenticationRequired();
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
    /// Authorizes the caller against the specified resource, returning the failure result when the caller is not
    /// authorized so a route can refuse before it reads anything.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="authorizationResource">The resource, such as a node, to authorize against.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <c>401</c> or <c>403</c> <see cref="IResult"/> when the caller is not authorized; otherwise
    /// <c>null</c>.</returns>
    protected internal virtual async ValueTask<IResult?> GetAuthorizationFailureAsync(
        HttpContext httpContext,
        object authorizationResource,
        IAuthorizationRequirement authorizationRequirement,
        CancellationToken cancellationToken
    )
    {
        var authorizationResult = await AuthorizationService.AuthorizeAsync(
            httpContext.User,
            authorizationResource,
            authorizationRequirement
        );

        return authorizationResult.Succeeded ? null : AuthorizationFailed(httpContext);
    }

    /// <summary>
    /// Processes an HTTP <c>GET</c> request: authorizes the caller against the specified resource, and only then reads
    /// the value and maps it to a response. The value is read only after authorization succeeds, so an unauthorized
    /// request never touches the store. Emits an <c>ETag</c> and honors <c>If-None-Match</c> when the value supports a
    /// concurrency token. When <paramref name="readAsync"/> returns <c>null</c> the request returns the <c>404 Not
    /// Found</c> problem.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="authorizationResource">The resource, such as a node, to authorize against.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate.</param>
    /// <param name="readAsync">A function that reads the value, or returns <c>null</c> when not found. It runs only
    /// after authorization succeeds.</param>
    /// <param name="mapper">A function that maps the value to the response representation, or returns <c>null</c> to
    /// report the mapped sub-resource as absent.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TValue">The type of the value being processed.</typeparam>
    /// <typeparam name="TResponse">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        object authorizationResource,
        IAuthorizationRequirement authorizationRequirement,
        Func<CancellationToken, ValueTask<TValue?>> readAsync,
        Func<TValue, TResponse> mapper,
        CancellationToken cancellationToken
    ) =>
        ProcessGetAsync(
            httpContext,
            authorizationResource,
            authorizationRequirement,
            readAsync,
            (value, _) => ValueTask.FromResult(mapper(value)),
            cancellationToken
        );

    /// <summary>
    /// Processes an HTTP <c>GET</c> request: authorizes the caller against the specified resource, and only then reads
    /// the value and maps it to a response. The value is read only after authorization succeeds, so an unauthorized
    /// request never touches the store. Emits an <c>ETag</c> and honors <c>If-None-Match</c> when the value supports a
    /// concurrency token. When <paramref name="readAsync"/> returns <c>null</c> or <paramref name="mapperAsync"/>
    /// returns <c>null</c>, the request returns the <c>404 Not Found</c> problem.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="authorizationResource">The resource, such as a node, to authorize against.</param>
    /// <param name="authorizationRequirement">The <see cref="IAuthorizationRequirement"/> to evaluate.</param>
    /// <param name="readAsync">A function that reads the value, or returns <c>null</c> when not found. It runs only
    /// after authorization succeeds.</param>
    /// <param name="mapperAsync">A function that asynchronously maps the value to the response representation, or
    /// returns <c>null</c> to report the mapped sub-resource as absent. It runs only after authorization succeeds.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TValue">The type of the value being processed.</typeparam>
    /// <typeparam name="TResponse">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(
        HttpContext httpContext,
        object authorizationResource,
        IAuthorizationRequirement authorizationRequirement,
        Func<CancellationToken, ValueTask<TValue?>> readAsync,
        Func<TValue, CancellationToken, ValueTask<TResponse>> mapperAsync,
        CancellationToken cancellationToken
    )
    {
        var failure = await GetAuthorizationFailureAsync(
            httpContext,
            authorizationResource,
            authorizationRequirement,
            cancellationToken
        );

        if (failure is not null)
        {
            return failure;
        }

        var value = await readAsync(cancellationToken);
        if (value is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        // Project first so an absent sub-resource short-circuits to 404 before emitting an ETag or a 304.
        var response = await mapperAsync(value, cancellationToken);
        if (response is null)
        {
            return ManagementProblems.ResourceNotFound();
        }

        if (value is ISupportConcurrencyToken supportConcurrencyToken)
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
    /// <param name="authorizationResource">The node the list is authorized at with <see cref="Operations.List"/>,
    /// such as the tenant scope for a tenant's own family or the platform node for a platform-wide list, so a list is
    /// never authorized without a node.</param>
    /// <param name="fetchPageAsync">A function that fetches the page once authorization has succeeded.</param>
    /// <param name="mapper">A function that maps a persisted item to its response representation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TItem">The type of the persisted item.</typeparam>
    /// <typeparam name="TResource">The type of the response representation.</typeparam>
    /// <returns>An <see cref="IResult"/> representing the outcome of the request.</returns>
    protected internal virtual async ValueTask<IResult> ProcessListAsync<TItem, TResource>(
        HttpContext httpContext,
        object authorizationResource,
        Func<ValueTask<PagedResult<TItem>>> fetchPageAsync,
        Func<TItem, TResource> mapper,
        CancellationToken cancellationToken
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
