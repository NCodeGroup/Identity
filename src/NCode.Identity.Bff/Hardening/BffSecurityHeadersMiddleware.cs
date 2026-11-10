// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Hardening;

/// <summary>
/// Writes the security headers of the frontend that serves the request on every response, just before it starts,
/// unless the application already set the header. Writing them as the response starts keeps them on a response that an
/// exception handler cleared.
/// </summary>
internal class BffSecurityHeadersMiddleware(RequestDelegate next, BffFrontendCatalog frontends)
{
    private RequestDelegate Next { get; } = next;
    private BffFrontendCatalog Frontends { get; } = frontends;

    /// <summary>
    /// Runs the middleware for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A request that no frontend selects still gets the strict defaults.
        var headers = Frontends.Resolve(context)?.SecurityHeaders ?? BffSecurityHeaders.Default;

        context.Response.OnStarting(WriteHeaders, (context.Response, headers));
        return Next(context);
    }

    private static Task WriteHeaders(object state)
    {
        var (response, headers) = ((
            HttpResponse,
            IReadOnlyList<KeyValuePair<string, string>>
        ))state;
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAdd(name, value);
        }

        return Task.CompletedTask;
    }
}
