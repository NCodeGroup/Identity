// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Hardening;

/// <summary>
/// Writes the configured security headers on every response, just before it starts, unless the application already set
/// the header. Writing them as the response starts keeps them on a response that an exception handler cleared.
/// </summary>
internal class BffSecurityHeadersMiddleware(RequestDelegate next, IOptions<BffOptions> options)
{
    private const string NoSniff = "nosniff";
    private const string ReferrerPolicyHeaderName = "Referrer-Policy";
    private const string CrossOriginOpenerPolicyHeaderName = "Cross-Origin-Opener-Policy";

    private readonly RequestDelegate _next = next;
    private readonly KeyValuePair<string, string>[] _headers = CreateHeaders(
        options.Value.SecurityHeaders
    );

    /// <summary>
    /// Runs the middleware for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(WriteHeaders, context.Response);
        return _next(context);
    }

    private Task WriteHeaders(object state)
    {
        var headers = ((HttpResponse)state).Headers;
        foreach (var (name, value) in _headers)
        {
            headers.TryAdd(name, value);
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
