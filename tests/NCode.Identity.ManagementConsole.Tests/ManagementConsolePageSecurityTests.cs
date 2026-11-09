// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsolePageSecurityTests
{
    private static Uri Authority { get; } = new("https://id.example");

    #region Create Tests

    [Fact]
    public void Create_WithoutALogo_WritesTheStrictPolicy()
    {
        var security = ManagementConsolePageSecurity.Create(Authority, logoUrl: null);

        var nonce = security.Nonce;
        Assert.Equal(
            $"default-src 'none'; script-src 'nonce-{nonce}' 'strict-dynamic'; style-src 'nonce-{nonce}'; img-src 'self'; connect-src 'self'; form-action 'self' https://id.example; base-uri 'none'; frame-ancestors 'none'",
            security.Policy.ToContentSecurityPolicy()
        );
    }

    [Fact]
    public void Create_WithAnAbsoluteLogo_AllowsTheLogosOriginAsAnImageSource()
    {
        var security = ManagementConsolePageSecurity.Create(
            Authority,
            "https://cdn.example:8443/logo.png"
        );

        Assert.Contains(
            "img-src 'self' https://cdn.example:8443;",
            security.Policy.ToContentSecurityPolicy(),
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("/logo.png")]
    public void Create_WithALogoThatIsNotAbsolute_AllowsOnlyTheConsolesOwnImages(string logoUrl)
    {
        var security = ManagementConsolePageSecurity.Create(Authority, logoUrl);

        Assert.Contains(
            "img-src 'self';",
            security.Policy.ToContentSecurityPolicy(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Create_Always_CreatesAFreshNonce()
    {
        var first = ManagementConsolePageSecurity.Create(Authority, logoUrl: null);
        var second = ManagementConsolePageSecurity.Create(Authority, logoUrl: null);

        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    #endregion
}
