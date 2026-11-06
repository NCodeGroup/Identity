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
    /// fresh nonce, the console's own images and API calls, the origin of the branding's logo, and form submissions
    /// to the console and to its authority, which signs the user in. It forbids a base URI and framing.
    /// </summary>
    /// <param name="authority">The authority that the console's BFF signs users in with.</param>
    /// <param name="logoUrl">The branding's logo URL, or <see langword="null"/> when the console has none.</param>
    /// <returns>The nonce and the policy of the response.</returns>
    public static ManagementConsolePageSecurity Create(Uri authority, string? logoUrl)
    {
        var nonce = ContentSecurityPolicySources.CreateNonce();

        List<string> imageSources = [ContentSecurityPolicySources.Self];
        if (AbsoluteUri.TryParse(logoUrl, out var logoUri))
        {
            imageSources.Add(ContentSecurityPolicySources.Origin(logoUri));
        }

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
}
