// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.AspNetCore.Options;
using Xunit;

namespace NCode.AspNetCore.Tests.Options;

public sealed class TrustedForwardedHeadersOptionsValidatorTests
{
    private static TrustedForwardedHeadersOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenDefaults_Succeeds()
    {
        var result = Validator.Validate(name: null, new TrustedForwardedHeadersOptions());

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
            new TrustedForwardedHeadersOptions { KnownProxies = [proxy] }
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
            new TrustedForwardedHeadersOptions { KnownProxies = ["10.0.0.4", proxy] }
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            $"ForwardedHeaders:KnownProxies:1 '{proxy}'",
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
            new TrustedForwardedHeadersOptions { KnownNetworks = [network] }
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
            new TrustedForwardedHeadersOptions { KnownNetworks = [network] }
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            $"ForwardedHeaders:KnownNetworks:0 '{network}'",
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
            new TrustedForwardedHeadersOptions { ForwardLimit = forwardLimit }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ForwardedHeaders:ForwardLimit",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenSeveralEntriesAreInvalid_ReportsEachOne()
    {
        var result = Validator.Validate(
            name: null,
            new TrustedForwardedHeadersOptions
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
