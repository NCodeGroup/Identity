// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Hardening;

/// <summary>
/// Writes the security headers of the frontend that serves the request on every response, just before it starts,
/// unless the application already set the header. Writing them as the response starts keeps them on a response that an
/// exception handler cleared.
/// </summary>
internal class BffSecurityHeadersMiddleware(RequestDelegate next, BffFrontendCatalog frontends)
{
    private const string NoSniff = "nosniff";
    private const string ReferrerPolicyHeaderName = "Referrer-Policy";
    private const string CrossOriginOpenerPolicyHeaderName = "Cross-Origin-Opener-Policy";

    private static KeyValuePair<string, string>[] DefaultHeaders { get; } =
        CreateHeaders(new BffSecurityHeadersOptions());

    private readonly RequestDelegate _next = next;
    private readonly BffFrontendCatalog _frontends = frontends;

    private readonly FrozenDictionary<string, KeyValuePair<string, string>[]> _headersByFrontend =
        frontends.Frontends.ToFrozenDictionary(
            x => x.Name,
            x => CreateHeaders(x.Options.SecurityHeaders),
            StringComparer.Ordinal
        );

    /// <summary>
    /// Runs the middleware for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A request that no frontend selects still gets the strict defaults.
        var frontend = _frontends.Resolve(context);
        var headers =
            frontend is not null && _headersByFrontend.TryGetValue(frontend.Name, out var found)
                ? found
                : DefaultHeaders;

        context.Response.OnStarting(WriteHeaders, (context.Response, headers));
        return _next(context);
    }

    private static Task WriteHeaders(object state)
    {
        var (response, headers) = ((HttpResponse, KeyValuePair<string, string>[]))state;
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAdd(name, value);
        }

        return Task.CompletedTask;
    }

    private static KeyValuePair<string, string>[] CreateHeaders(
        BffSecurityHeadersOptions securityHeaders
    ) =>
        [
            new(HeaderNames.ContentSecurityPolicy, securityHeaders.ContentSecurityPolicy),
            new(HeaderNames.XContentTypeOptions, NoSniff),
            new(ReferrerPolicyHeaderName, securityHeaders.ReferrerPolicy),
            new(CrossOriginOpenerPolicyHeaderName, securityHeaders.CrossOriginOpenerPolicy),
        ];
}
