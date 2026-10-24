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

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Holds what the request rate limiter records on one request: whether a stage refused it, and the usage meters of
/// every stage that admitted it. <see cref="RateLimitEndpointFilter"/> reads it once the endpoint has run, and records
/// the meters unless a stage refused the request. The state lives in <see cref="HttpContext.Items"/>, so it ends with
/// the request.
/// </summary>
// See ADR-0246.
internal sealed class RateLimitRequestState
{
    /// <summary>
    /// Gets or sets a value indicating whether a stage refused the request.
    /// </summary>
    public bool IsRefused { get; set; }

    /// <summary>
    /// Gets the names of the usage meters that the stages admitting the request added, each once, in the order they
    /// were first added.
    /// </summary>
    public List<string> UsageMeters { get; } = [];

    private static object ItemKey { get; } = new();

    /// <summary>
    /// Gets the state of a request, storing an empty state on the request first when it has none.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <returns>The <see cref="RateLimitRequestState"/> of the request.</returns>
    public static RateLimitRequestState Get(HttpContext httpContext)
    {
        if (
            httpContext.Items.TryGetValue(ItemKey, out var stored)
            && stored is RateLimitRequestState existing
        )
            return existing;

        var state = new RateLimitRequestState();
        httpContext.Items[ItemKey] = state;
        return state;
    }
}
