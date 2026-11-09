// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Creates the problem answers that every management route shares.
/// </summary>
[PublicAPI]
public static class ManagementProblems
{
    /// <summary>
    /// Creates the <c>401 Unauthorized</c> problem for a request that carries no valid management access token.
    /// </summary>
    public static ProblemHttpResult AuthenticationRequired() =>
        TypedResults.Problem(
            detail: "Authentication is required.",
            statusCode: StatusCodes.Status401Unauthorized
        );

    /// <summary>
    /// Creates the <c>404 Not Found</c> problem for a resource that the request names and that does not exist.
    /// </summary>
    public static ProblemHttpResult ResourceNotFound() =>
        TypedResults.Problem(
            detail: "The resource does not exist.",
            statusCode: StatusCodes.Status404NotFound
        );

    /// <summary>
    /// Creates the <c>412 Precondition Failed</c> problem for a request whose <c>If-Match</c> token does not match
    /// the current concurrency token of the resource it changes.
    /// </summary>
    /// <param name="resource">
    /// The resource whose concurrency token the request's <c>If-Match</c> token was compared with, such as
    /// <c>settings</c> or <c>secret</c>, which the problem's detail names.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="resource"/> is <see langword="null"/>, empty or white space.
    /// </exception>
    public static ProblemHttpResult PreconditionFailed(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        return TypedResults.Problem(
            detail: $"The supplied If-Match token does not match the current {resource}.",
            statusCode: StatusCodes.Status412PreconditionFailed
        );
    }
}
