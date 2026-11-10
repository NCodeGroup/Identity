// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.AspNetCore;
using NCode.Identity.OpenId;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The security of one console page response: the nonce that the page's scripts and styles carry and the
/// Content-Security-Policy that allows only that nonce. A response creates its own, so the nonce is never reused.
/// </summary>
/// <param name="Nonce">The nonce of the page's policy, written in place of the bundle's nonce placeholder.</param>
/// <param name="Policy">The policy that the page is served under.</param>
internal readonly record struct ManagementConsolePageSecurity(
    string Nonce,
    HtmlPageSecurityPolicy Policy
)
{
    /// <summary>
    /// Creates the security of one console page response. The policy allows the page's scripts and styles only with a
    /// fresh nonce, the console's own images and API calls, the origins of the branding's logo and tab icon, and form
    /// submissions to the console and to its authority, which signs the user in. It forbids a base URI and framing.
    /// </summary>
    /// <param name="authority">The authority that the console's BFF signs users in with.</param>
    /// <param name="logoUrl">The branding's logo URL, or <see langword="null"/> when the console has none.</param>
    /// <param name="faviconUrl">The branding's tab icon URL, or <see langword="null"/> when the console has none.</param>
    /// <returns>The nonce and the policy of the response.</returns>
    public static ManagementConsolePageSecurity Create(
        Uri authority,
        string? logoUrl,
        string? faviconUrl
    )
    {
        var nonce = ContentSecurityPolicySources.CreateNonce();

        List<string> imageSources = [ContentSecurityPolicySources.Self];
        AddImageSourceOrigin(imageSources, logoUrl);
        AddImageSourceOrigin(imageSources, faviconUrl);

        var policy = new HtmlPageSecurityPolicy
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Nonce(nonce),
                    ContentSecurityPolicySources.StrictDynamic
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.StyleSrc,
                    ContentSecurityPolicySources.Nonce(nonce)
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ImgSrc,
                    imageSources
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ConnectSrc,
                    ContentSecurityPolicySources.Self
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.FormAction,
                    ContentSecurityPolicySources.Self,
                    ContentSecurityPolicySources.Origin(authority)
                ),
            ],
        };

        return new ManagementConsolePageSecurity(nonce, policy);
    }

    private static void AddImageSourceOrigin(List<string> imageSources, string? url)
    {
        if (!AbsoluteUri.TryParse(url, out var uri))
        {
            return;
        }

        var origin = ContentSecurityPolicySources.Origin(uri);
        if (!imageSources.Contains(origin))
        {
            imageSources.Add(origin);
        }
    }
}
