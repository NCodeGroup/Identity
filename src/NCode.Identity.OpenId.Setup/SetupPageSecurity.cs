// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// The security of one setup page response: the nonce that the page's scripts and styles carry and the
/// Content-Security-Policy that allows only that nonce. A response creates its own, so the nonce is never reused.
/// </summary>
/// <param name="Nonce">The nonce of the page's policy, written in place of the bundle's nonce placeholder.</param>
/// <param name="Policy">The policy that the page is served under.</param>
internal readonly record struct SetupPageSecurity(string Nonce, HtmlPageSecurityPolicy Policy)
{
    /// <summary>
    /// Creates the security of one setup page response. The policy allows the page's scripts and styles only with a
    /// fresh nonce, and the page's own images and API calls. It allows a base URI of the page's own origin, because
    /// the page carries a same-origin <c>base</c> element, and it forbids form submission and framing.
    /// </summary>
    /// <returns>The nonce and the policy of the response.</returns>
    public static SetupPageSecurity Create()
    {
        var nonce = ContentSecurityPolicySources.CreateNonce();

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
                    ContentSecurityPolicySources.Self
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
        };

        return new SetupPageSecurity(nonce, policy);
    }
}
