// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractivePageSecurityTests
{
    #region Create Tests

    [Fact]
    public void Create_WhenNoBrandingIsGiven_AllowsTheNonceAndTheSurfaceOnly()
    {
        var security = InteractivePageSecurity.Create(null);

        var n = security.Nonce;
        Assert.Equal(
            $"default-src 'none'; script-src 'nonce-{n}' 'strict-dynamic'; style-src 'nonce-{n}'; img-src 'self'; connect-src 'self'; base-uri 'self'; frame-ancestors 'none'",
            security.Policy.ToContentSecurityPolicy()
        );
    }

    [Fact]
    public void Create_WhenTheBrandingHasALogo_AllowsTheLogosOrigin()
    {
        var branding = new InteractiveBranding(
            null,
            "https://cdn.example.com/acme/logo.png",
            null,
            null,
            null,
            null
        );

        var security = InteractivePageSecurity.Create(branding);

        Assert.Contains(
            "img-src 'self' https://cdn.example.com;",
            security.Policy.ToContentSecurityPolicy()
        );
    }

    [Fact]
    public void Create_WhenTheLogoIsNotAnAbsoluteUri_AllowsOnlyTheSurfacesImages()
    {
        var branding = new InteractiveBranding(null, "logo.png", null, null, null, null);

        var security = InteractivePageSecurity.Create(branding);

        Assert.Contains("img-src 'self';", security.Policy.ToContentSecurityPolicy());
    }

    [Fact]
    public void Create_Always_UsesAFreshNonce()
    {
        var first = InteractivePageSecurity.Create(null);
        var second = InteractivePageSecurity.Create(null);

        Assert.Matches(@"^[A-Za-z0-9_-]{22}\z", first.Nonce);
        Assert.Matches(@"^[A-Za-z0-9_-]{22}\z", second.Nonce);
        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    #endregion
}
