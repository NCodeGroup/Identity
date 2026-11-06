// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using NCode.AspNetCore;
using NCode.Buffers;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.Authentication;

/// <summary>
/// Decides whether a page navigation that reaches a <c>SameSite=Strict</c> frontend without its session cookie is
/// answered with a same-origin reload page, and writes that page.
/// </summary>
/// <remarks>
/// A <c>SameSite=Strict</c> session cookie is withheld on a top-level navigation that a cross-site page or redirect
/// chain starts, such as the redirect from the callback endpoint to the return URL, which a browser labels as
/// <c>Sec-Fetch-Site: cross-site</c> or <c>none</c> depending on the browser, so Fetch Metadata cannot tell it apart.
/// The page answers once with a reload page that points at the request's own URL, which the browser follows as a
/// same-origin navigation that carries the cookie, and the <c>SameSite=Lax</c> marker cookie limits the reload to once.
/// </remarks>
/// <param name="cookies">The BFF cookies, which hold the session cookie and the reload marker.</param>
// See ADR-1444 and ADR-1256.
internal sealed class BffPageReload(BffCookies cookies)
{
    private const string SecFetchModeHeaderName = "Sec-Fetch-Mode";
    private const string NavigateFetchMode = "navigate";
    private const string ContentType = "text/html; charset=utf-8";
    private const string UrlPlaceholder = "{url}";
    private const string PageTemplate =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"refresh\" content=\"0;url={url}\"><title>Continue</title></head><body><a href=\"{url}\">Continue</a></body></html>";

    // The policy lists no directive, so the page allows nothing: default-src, base-uri, form-action and
    // frame-ancestors are all 'none'.
    private static readonly HtmlPageSecurityPolicy Policy = new();

    private BffCookies Cookies { get; } = cookies;

    /// <summary>
    /// Determines whether the request is answered with the reload page: a <c>GET</c> or <c>HEAD</c> navigation to a
    /// <c>SameSite=Strict</c> frontend that carries neither the session cookie nor the reload marker, whose own URL
    /// is a local URL.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="frontend">The frontend that the request belongs to.</param>
    /// <returns><see langword="true"/> when the request is answered with the reload page.</returns>
    public bool ShouldReload(HttpContext context, BffFrontend frontend)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(frontend);

        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        var fetchMode = request.Headers[SecFetchModeHeaderName].ToString();
        if (
            fetchMode.Length != 0
            && !string.Equals(fetchMode, NavigateFetchMode, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        return frontend.Options.Session.SameSite == SameSiteMode.Strict
            && Cookies.ReadSessionHandle(request, frontend) is null
            && !Cookies.HasPageReload(request, frontend)
            && LocalUrl.IsLocal(GetOwnUrl(request));
    }

    /// <summary>
    /// Answers the request with the reload page and sets the marker cookie that limits the reload to once. The
    /// response carries the shared page-security headers, and a <c>HEAD</c> request gets the headers and no body.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
    /// <param name="frontend">The frontend that the request belongs to.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    public async ValueTask WriteAsync(
        HttpContext context,
        BffFrontend frontend,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(frontend);

        var request = context.Request;
        var response = context.Response;
        Cookies.SetPageReload(response, frontend);

        response.StatusCode = StatusCodes.Status200OK;
        response.SetHtmlPageSecurity(Policy);
        response.ContentType = ContentType;

        if (HttpMethods.IsHead(request.Method))
            return;

        var encoded = HtmlEncoder.Default.Encode(GetOwnUrl(request));
        var body = PageTemplate.Replace(UrlPlaceholder, encoded, StringComparison.Ordinal);
        await response.WriteAsync(body, SecureEncoding.UTF8, cancellationToken);
    }

    private static string GetOwnUrl(HttpRequest request) =>
        $"{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}{request.QueryString.ToUriComponent()}";
}
