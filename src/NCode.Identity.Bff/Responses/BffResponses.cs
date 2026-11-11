// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Serialization;

namespace NCode.Identity.Bff.Responses;

/// <summary>
/// Writes the BFF's problem responses, which are never cached.
/// </summary>
internal static class BffResponses
{
    private const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// Writes an <c>application/problem+json</c> response that is never cached.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="status">The HTTP status code.</param>
    /// <param name="type">The problem type URN.</param>
    /// <param name="title">The short summary of the problem.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static ValueTask WriteProblemAsync(
        HttpContext httpContext,
        int status,
        string type,
        string title,
        CancellationToken cancellationToken
    )
    {
        var response = httpContext.Response;
        response.SetNoStore();
        response.StatusCode = status;

        var problem = new BffProblem(type, title, status);
        return new ValueTask(
            response.WriteAsJsonAsync(
                problem,
                BffJsonContext.Default.BffProblem,
                ProblemContentType,
                cancellationToken
            )
        );
    }

    /// <summary>
    /// Writes the problem that answers a token requirement's failure: <c>session_required</c> for a <c>401</c>, and
    /// <c>token_unavailable</c> for any other status.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="status">The HTTP status code of the failure.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static ValueTask WriteTokenUnavailableAsync(
        HttpContext httpContext,
        int status,
        CancellationToken cancellationToken
    )
    {
        var type =
            status == StatusCodes.Status401Unauthorized
                ? BffProblemTypes.SessionRequired
                : BffProblemTypes.TokenUnavailable;

        return WriteProblemAsync(
            httpContext,
            status,
            type,
            "An access token is unavailable.",
            cancellationToken
        );
    }
}
