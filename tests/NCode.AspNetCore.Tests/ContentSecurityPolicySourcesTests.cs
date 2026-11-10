// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Xunit;

namespace NCode.AspNetCore.Tests;

public class ContentSecurityPolicySourcesTests : BaseTests
{
    #region Origin Tests

    [Theory]
    [InlineData("https://Cdn.Example.com/logo.png", "https://cdn.example.com")]
    [InlineData("https://rp.example:8443/out", "https://rp.example:8443")]
    [InlineData("https://rp.example:443/out", "https://rp.example")]
    [InlineData("http://127.0.0.1:5174/cb", "http://127.0.0.1:5174")]
    [InlineData("https://b\u00fccher.example/", "https://xn--bcher-kva.example")]
    [InlineData("https://[::1]:8443/x", "https:")]
    [InlineData("com.example.app:/callback", "com.example.app:")]
    public void Origin_WhenGivenAUri_ReturnsTheSourceOfItsOrigin(string uri, string expected)
    {
        var actual = ContentSecurityPolicySources.Origin(new Uri(uri, UriKind.Absolute));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Origin_WhenTheUriIsRelative_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ContentSecurityPolicySources.Origin(new Uri("/logo.png", UriKind.Relative))
        );
    }

    #endregion

    #region AddOrigin Tests

    [Fact]
    public void AddOrigin_WhenTheUriIsNull_LeavesTheSourcesUnchanged()
    {
        var sources = new List<string> { ContentSecurityPolicySources.Self };

        ContentSecurityPolicySources.AddOrigin(sources, null);

        Assert.Equal([ContentSecurityPolicySources.Self], sources);
    }

    [Fact]
    public void AddOrigin_WhenTheOriginIsNotListed_AddsIt()
    {
        var sources = new List<string> { ContentSecurityPolicySources.Self };

        ContentSecurityPolicySources.AddOrigin(
            sources,
            new Uri("https://cdn.example.com/logo.png", UriKind.Absolute)
        );

        Assert.Equal([ContentSecurityPolicySources.Self, "https://cdn.example.com"], sources);
    }

    [Fact]
    public void AddOrigin_WhenTheOriginIsAlreadyListed_DoesNotAddItAgain()
    {
        var sources = new List<string> { "https://cdn.example.com" };

        ContentSecurityPolicySources.AddOrigin(
            sources,
            new Uri("https://CDN.example.com/icon.png", UriKind.Absolute)
        );

        Assert.Equal(["https://cdn.example.com"], sources);
    }

    [Fact]
    public void AddOrigin_WhenTheUriIsRelative_Throws()
    {
        var sources = new List<string>();

        Assert.Throws<ArgumentException>(() =>
            ContentSecurityPolicySources.AddOrigin(sources, new Uri("/logo.png", UriKind.Relative))
        );
    }

    #endregion

    #region Sha256 Tests

    [Fact]
    public void Sha256_WhenGivenAScript_ReturnsTheHashSource()
    {
        var actual = ContentSecurityPolicySources.Sha256("abc");

        Assert.Equal("'sha256-ungWv48Bz+pBQUDeXa4iI7ADYaOWF3qctBD/YfIAFa0='", actual);
    }

    #endregion

    #region Nonce Tests

    [Fact]
    public void Nonce_WhenGivenANonce_ReturnsTheNonceSource()
    {
        var actual = ContentSecurityPolicySources.Nonce("abc_-123");

        Assert.Equal("'nonce-abc_-123'", actual);
    }

    #endregion

    #region CreateNonce Tests

    [Fact]
    public void CreateNonce_Always_ReturnsAFreshBase64UrlValue()
    {
        var first = ContentSecurityPolicySources.CreateNonce();
        var second = ContentSecurityPolicySources.CreateNonce();

        Assert.Matches(new Regex(@"^[A-Za-z0-9_-]{22}\z"), first);
        Assert.Matches(new Regex(@"^[A-Za-z0-9_-]{22}\z"), second);
        Assert.NotEqual(first, second);
    }

    #endregion
}
