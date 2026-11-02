// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Server.ReverseProxy;
using Xunit;

namespace NCode.Identity.Server.Tests.ReverseProxy;

public sealed class ReverseProxyOptionsValidatorTests
{
    private static ReverseProxyOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenDefaults_Succeeds()
    {
        var result = Validator.Validate(name: null, new ReverseProxyOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("10.0.0.4")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:DB8::1")]
    public void Validate_WhenKnownProxyIsAnAddress_Succeeds(string proxy)
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions { KnownProxies = [proxy] }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData("10.0.0")]
    [InlineData("010.0.0.1")]
    [InlineData("10")]
    [InlineData("10.0.0.0/8")]
    public void Validate_WhenKnownProxyIsNotACanonicalAddress_FailsNamingTheEntry(string proxy)
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions { KnownProxies = ["10.0.0.4", proxy] }
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            $"ReverseProxy:KnownProxies:1 '{proxy}'",
            failure,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("172.16.0.0/12")]
    [InlineData("10.0.0.4/32")]
    [InlineData("2001:db8::/32")]
    public void Validate_WhenKnownNetworkIsInCidrNotation_Succeeds(string network)
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions { KnownNetworks = [network] }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("not-a-network")]
    [InlineData("172.16.0.0")]
    [InlineData("10.0.0.4/8")]
    [InlineData("10.0/8")]
    [InlineData("10.0.0.0/33")]
    public void Validate_WhenKnownNetworkIsNotInCidrNotation_FailsNamingTheEntry(string network)
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions { KnownNetworks = [network] }
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            $"ReverseProxy:KnownNetworks:0 '{network}'",
            failure,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenForwardLimitIsBelowOne_Fails(int forwardLimit)
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions { ForwardLimit = forwardLimit }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ReverseProxy:ForwardLimit",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenSeveralEntriesAreInvalid_ReportsEachOne()
    {
        var result = Validator.Validate(
            name: null,
            new ReverseProxyOptions
            {
                KnownProxies = ["bad"],
                KnownNetworks = ["bad"],
                ForwardLimit = 0,
            }
        );

        Assert.Equal(3, result.Failures?.Count());
    }

    #endregion
}
