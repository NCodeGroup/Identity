// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Serialization;

namespace NCode.Identity.Bff.Responses;

/// <summary>
/// Writes the BFF's uncacheable and problem responses.
/// </summary>
internal static class BffResponses
{
    private const string NoStore = "no-store";
    private const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// Marks the response as uncacheable with <c>Cache-Control: no-store</c>.
    /// </summary>
    /// <param name="response">The response to mark.</param>
    public static void SetNoStore(HttpResponse response)
    {
        response.Headers[HeaderNames.CacheControl] = NoStore;
    }

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
        SetNoStore(response);
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
