// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Creates the HTTP response of the <c>OpenID Connect</c> RP-Initiated Logout <c>end_session_endpoint</c> from an
/// <see cref="EndSessionDisposition"/>: the front-channel logout page, a redirect to the validated
/// <c>post_logout_redirect_uri</c>, or a logged-out confirmation.
/// </summary>
internal static class EndSessionHttpResults
{
    private const string RedirectScript =
        "window.addEventListener('load',function(){setTimeout(function(){window.location.href=document.getElementById('pl').getAttribute('data-url');},2000);});";

    private static string RedirectScriptSource { get; } =
        ContentSecurityPolicySources.Sha256(RedirectScript);

    /// <summary>
    /// Creates the response for a completed logout.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="disposition">The <see cref="EndSessionDisposition"/> that describes where the user agent goes.</param>
    public static IResult Create(HttpContext httpContext, EndSessionDisposition disposition)
    {
        // When front-channel logout URIs are present, the OP must render a page that loads each one in a hidden iframe
        // so the participating clients clear their own sessions before the user agent leaves the OP.
        if (disposition.FrontChannelLogoutUris.Count > 0)
        {
            var html = BuildFrontChannelLogoutHtml(
                disposition.FrontChannelLogoutUris,
                disposition.RedirectUri
            );
            httpContext.Response.SetHtmlPageSecurity(
                CreateFrontChannelLogoutPolicy(
                    disposition.FrontChannelLogoutUris,
                    disposition.RedirectUri
                )
            );
            return TypedResults.Content(html, "text/html; charset=utf-8", SecureEncoding.UTF8);
        }

        if (!string.IsNullOrEmpty(disposition.RedirectUri))
        {
            // See ADR-1203.
            return TypedResults.Redirect(RedirectLocation.Escape(disposition.RedirectUri));
        }

        return TypedResults.Content("You have been logged out.", "text/plain", SecureEncoding.UTF8);
    }

    // Builds the Front-Channel Logout 1.0 page: a hidden iframe per participating client, then a scripted redirect to
    // the validated post-logout URI (or a plain confirmation when none was supplied). All URIs are registered client
    // values but are HTML-encoded defensively before they are embedded in the markup. The page hides its elements with
    // the hidden attribute rather than a style attribute, so its Content-Security-Policy needs no style source.
    private static string BuildFrontChannelLogoutHtml(
        IReadOnlyList<string> frontChannelLogoutUris,
        string? redirectUri
    )
    {
        var encoder = HtmlEncoder.Default;
        var builder = new StringBuilder();
        builder.Append(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Logout</title></head><body>"
        );

        foreach (var uri in frontChannelLogoutUris)
        {
            builder
                .Append("<iframe hidden src=\"")
                .Append(encoder.Encode(uri))
                .Append("\"></iframe>");
        }

        if (!string.IsNullOrEmpty(redirectUri))
        {
            // Redirect only after the iframes have had a chance to load; the target is read from a DOM attribute so
            // the validated URI is never interpolated into an inline script literal.
            builder
                .Append("<a id=\"pl\" data-url=\"")
                .Append(encoder.Encode(redirectUri))
                .Append("\" hidden></a>")
                .Append("<script>" + RedirectScript + "</script>");
        }
        else
        {
            builder.Append("<p>You have been logged out.</p>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    // The page may frame only the origins of the logout URIs it renders, and may run only its own redirect script. The
    // page lists no form-action directive and posts no form, so the strict default applies. See ADR-1256.
    private static HtmlPageSecurityPolicy CreateFrontChannelLogoutPolicy(
        IReadOnlyList<string> frontChannelLogoutUris,
        string? redirectUri
    )
    {
        var frameSources = frontChannelLogoutUris
            .Select(uri =>
                AbsoluteUri.TryParse(uri, out var parsed)
                    ? ContentSecurityPolicySources.Origin(parsed)
                    : null
            )
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);

        List<ContentSecurityPolicyDirective> directives =
        [
            new(ContentSecurityPolicyDirectives.FrameSrc, frameSources),
        ];

        if (!string.IsNullOrEmpty(redirectUri))
        {
            directives.Add(new(ContentSecurityPolicyDirectives.ScriptSrc, RedirectScriptSource));
        }

        return new HtmlPageSecurityPolicy { Directives = [.. directives] };
    }
}
