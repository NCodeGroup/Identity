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
