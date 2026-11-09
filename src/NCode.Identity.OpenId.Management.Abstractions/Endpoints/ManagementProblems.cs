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
    /// Creates the <c>404 Not Found</c> problem for a resource that the request names and that does not exist.
    /// </summary>
    public static ProblemHttpResult ResourceNotFound() =>
        TypedResults.Problem(
            detail: "The resource does not exist.",
            statusCode: StatusCodes.Status404NotFound
        );
}
