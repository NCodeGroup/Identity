// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.AspNetCore;
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
    public static Task WriteProblemAsync(
        HttpContext httpContext,
        int status,
        string type,
        string title
    )
    {
        var response = httpContext.Response;
        response.SetNoStore();
        response.StatusCode = status;

        var problem = new BffProblem(type, title, status);
        return response.WriteAsJsonAsync(
            problem,
            BffJsonContext.Default.BffProblem,
            ProblemContentType,
            httpContext.RequestAborted
        );
    }
}
