// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The security of one interactive page response: the nonce that the page's scripts and styles carry and the
/// Content-Security-Policy that allows only that nonce. A response creates its own, so the nonce is never reused.
/// </summary>
/// <param name="Nonce">The nonce of the page's policy, written in place of the bundle's nonce placeholder.</param>
/// <param name="Policy">The policy that the page is served under.</param>
// See ADR-1256.
internal readonly record struct InteractivePageSecurity(string Nonce, HtmlPageSecurityPolicy Policy)
{
    /// <summary>
    /// Creates the security of one interactive page response. The policy allows the page's scripts and styles only
    /// with a fresh nonce, the surface's own images and API calls, and the origins of the tenant's logo and tab icon.
    /// </summary>
    /// <param name="branding">The tenant's branding, or <see langword="null"/> when the page has none.</param>
    /// <returns>The nonce and the policy of the response.</returns>
    public static InteractivePageSecurity Create(InteractiveBranding? branding)
    {
        var nonce = ContentSecurityPolicySources.CreateNonce();

        List<string> imageSources = [ContentSecurityPolicySources.Self];
        var logoUri = AbsoluteUri.TryParse(branding?.LogoUrl, out var parsedLogoUri)
            ? parsedLogoUri
            : null;
        ContentSecurityPolicySources.AddOrigin(imageSources, logoUri);
        ContentSecurityPolicySources.AddOrigin(imageSources, branding?.FaviconUri);

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
                    ContentSecurityPolicyDirectives.BaseUri,
                    ContentSecurityPolicySources.Self
                ),
            ],
            // Login, consent and logout posts redirect to the client and then anywhere the client chooses.
            AllowsAnyFormAction = true,
        };

        return new InteractivePageSecurity(nonce, policy);
    }
}
