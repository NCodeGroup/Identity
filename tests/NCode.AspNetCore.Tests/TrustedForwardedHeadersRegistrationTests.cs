// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.AspNetCore.Options;
using Xunit;
// Both System.Net and Microsoft.AspNetCore.HttpOverrides declare an IPNetwork; KnownIPNetworks takes the former.
using IPNetwork = System.Net.IPNetwork;

namespace NCode.AspNetCore.Tests;

public sealed class TrustedForwardedHeadersRegistrationTests : BaseTests
{
    #region AddTrustedForwardedHeaders Tests

    [Fact]
    public void AddTrustedForwardedHeaders_WhenNotConfigured_TrustsNoProxy()
    {
        var (options, forwarded) = Resolve([]);

        Assert.False(options.HasTrustedProxy);

        // The framework default trusts loopback; leaving that in place would mean "no configuration" still trusts a
        // peer. Both allow-lists must be empty so forwarded headers are ignored outright.
        Assert.Empty(forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
    }

    [Fact]
    public void AddTrustedForwardedHeaders_WhenKnownProxyConfigured_TrustsOnlyThatAddress()
    {
        var (options, forwarded) = Resolve(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.4",
                ["ForwardedHeaders:KnownProxies:1"] = "10.0.0.5",
            }
        );

        Assert.True(options.HasTrustedProxy);
        Assert.Equal(
            [IPAddress.Parse("10.0.0.4"), IPAddress.Parse("10.0.0.5")],
            forwarded.KnownProxies
        );
    }

    [Fact]
    public void AddTrustedForwardedHeaders_WhenKnownNetworkConfigured_TrustsThatNetwork()
    {
        var (options, forwarded) = Resolve(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "172.16.0.0/12",
            }
        );

        Assert.True(options.HasTrustedProxy);
        Assert.Equal([IPNetwork.Parse("172.16.0.0/12")], forwarded.KnownIPNetworks);
    }

    [Theory]
    [InlineData("KnownProxies", "not-an-address")]
    [InlineData("KnownProxies", "10.0.0")]
    [InlineData("KnownNetworks", "not-a-network")]
    [InlineData("KnownNetworks", "10.0.0.4/8")]
    public void AddTrustedForwardedHeaders_WhenEntryCannotBeHonored_Throws(string key, string entry)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            Resolve(new Dictionary<string, string?> { [$"ForwardedHeaders:{key}:0"] = entry })
        );

        Assert.Contains(
            $"ForwardedHeaders:{key}:0 '{entry}'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddTrustedForwardedHeaders_WhenForwardLimitNotConfigured_AllowsOneHop()
    {
        var (options, forwarded) = Resolve([]);

        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(1, forwarded.ForwardLimit);
    }

    [Fact]
    public void AddTrustedForwardedHeaders_WhenForwardLimitConfigured_UsesIt()
    {
        var (_, forwarded) = Resolve(
            new Dictionary<string, string?> { ["ForwardedHeaders:ForwardLimit"] = "2" }
        );

        Assert.Equal(2, forwarded.ForwardLimit);
    }

    [Fact]
    public void AddTrustedForwardedHeaders_Always_ForwardsForProtoAndHost()
    {
        var (_, forwarded) = Resolve([]);

        Assert.Equal(
            ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost,
            forwarded.ForwardedHeaders
        );
    }

    [Fact]
    public void AddTrustedForwardedHeaders_WhenSectionHasUnknownKey_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Resolve(
                new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxy:0"] = "10.0.0.4" }
            )
        );

        Assert.Contains("'ForwardedHeaders'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'KnownProxy'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region UseTrustedForwardedHeaders Tests

    [Fact]
    public void UseTrustedForwardedHeaders_WhenNoTrustedProxy_AddsNoMiddleware()
    {
        var serviceProvider = BuildServiceProvider([]);

        // Strict: the mock has no Use(...) setup, so adding middleware would fail the test. That IS the assertion —
        // without a trusted proxy, forwarded headers must never be honored.
        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();

        var result = app.Object.UseTrustedForwardedHeaders();

        Assert.Same(app.Object, result);
    }

    [Fact]
    public void UseTrustedForwardedHeaders_WhenTrustedProxyConfigured_AddsMiddleware()
    {
        var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.4" }
        );

        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();
        app.Setup(x => x.Properties).Returns(new Dictionary<string, object?>()).Verifiable();
        app.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(app.Object)
            .Verifiable();

        var result = app.Object.UseTrustedForwardedHeaders();

        Assert.Same(app.Object, result);
    }

    #endregion

    private static ServiceProvider BuildServiceProvider(
        Dictionary<string, string?> configurationData
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddTrustedForwardedHeaders(configuration);

        return serviceCollection.BuildServiceProvider();
    }

    private static (
        TrustedForwardedHeadersOptions Options,
        ForwardedHeadersOptions Forwarded
    ) Resolve(Dictionary<string, string?> configurationData)
    {
        using var serviceProvider = BuildServiceProvider(configurationData);

        return (
            serviceProvider.GetRequiredService<IOptions<TrustedForwardedHeadersOptions>>().Value,
            serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value
        );
    }
}
