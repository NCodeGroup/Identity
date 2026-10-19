#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Server.ReverseProxy;
using Xunit;
// Both System.Net and Microsoft.AspNetCore.HttpOverrides declare an IPNetwork; KnownIPNetworks takes the former.
using IPNetwork = System.Net.IPNetwork;

namespace NCode.Identity.Server.Tests.ReverseProxy;

public sealed class ReverseProxyRegistrationTests : BaseTests
{
    #region AddReverseProxy Tests

    [Fact]
    public void AddReverseProxy_WhenNotConfigured_TrustsNoProxy()
    {
        var (options, forwarded) = Resolve([]);

        Assert.False(options.HasTrustedProxy);

        // The framework default trusts loopback; leaving that in place would mean "no configuration" still trusts a
        // peer. Both allow-lists must be empty so forwarded headers are ignored outright.
        Assert.Empty(forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
    }

    [Fact]
    public void AddReverseProxy_WhenKnownProxyConfigured_TrustsOnlyThatAddress()
    {
        var (options, forwarded) = Resolve(
            new Dictionary<string, string?>
            {
                ["ReverseProxy:KnownProxies:0"] = "10.0.0.4",
                ["ReverseProxy:KnownProxies:1"] = "10.0.0.5",
            }
        );

        Assert.True(options.HasTrustedProxy);
        Assert.Equal(
            [IPAddress.Parse("10.0.0.4"), IPAddress.Parse("10.0.0.5")],
            forwarded.KnownProxies
        );
    }

    [Fact]
    public void AddReverseProxy_WhenKnownNetworkConfigured_TrustsThatNetwork()
    {
        var (options, forwarded) = Resolve(
            new Dictionary<string, string?> { ["ReverseProxy:KnownNetworks:0"] = "172.16.0.0/12" }
        );

        Assert.True(options.HasTrustedProxy);
        Assert.Equal([IPNetwork.Parse("172.16.0.0/12")], forwarded.KnownIPNetworks);
    }

    [Theory]
    [InlineData("KnownProxies", "not-an-address")]
    [InlineData("KnownProxies", "10.0.0")]
    [InlineData("KnownNetworks", "not-a-network")]
    [InlineData("KnownNetworks", "10.0.0.4/8")]
    public void AddReverseProxy_WhenEntryCannotBeHonored_Throws(string key, string entry)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            Resolve(new Dictionary<string, string?> { [$"ReverseProxy:{key}:0"] = entry })
        );

        Assert.Contains(
            $"ReverseProxy:{key}:0 '{entry}'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddReverseProxy_WhenForwardLimitNotConfigured_AllowsOneHop()
    {
        var (options, forwarded) = Resolve([]);

        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(1, forwarded.ForwardLimit);
    }

    [Fact]
    public void AddReverseProxy_WhenForwardLimitConfigured_UsesIt()
    {
        var (_, forwarded) = Resolve(
            new Dictionary<string, string?> { ["ReverseProxy:ForwardLimit"] = "2" }
        );

        Assert.Equal(2, forwarded.ForwardLimit);
    }

    [Fact]
    public void AddReverseProxy_Always_ForwardsForProtoAndHost()
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
    public void AddReverseProxy_WhenSectionHasUnknownKey_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Resolve(new Dictionary<string, string?> { ["ReverseProxy:KnownProxy:0"] = "10.0.0.4" })
        );

        Assert.Contains("'ReverseProxy'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'KnownProxy'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region UseReverseProxy Tests

    [Fact]
    public void UseReverseProxy_WhenNoTrustedProxy_AddsNoMiddleware()
    {
        var serviceProvider = BuildServiceProvider([]);

        // Strict: the mock has no Use(...) setup, so adding middleware would fail the test. That IS the assertion —
        // without a trusted proxy, forwarded headers must never be honored.
        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();

        var result = app.Object.UseReverseProxy();

        Assert.Same(app.Object, result);
    }

    [Fact]
    public void UseReverseProxy_WhenTrustedProxyConfigured_AddsMiddleware()
    {
        var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "10.0.0.4" }
        );

        var app = CreateStrictMock<IApplicationBuilder>();
        app.Setup(x => x.ApplicationServices).Returns(serviceProvider).Verifiable();
        app.Setup(x => x.Properties).Returns(new Dictionary<string, object?>()).Verifiable();
        app.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(app.Object)
            .Verifiable();

        var result = app.Object.UseReverseProxy();

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
        serviceCollection.AddReverseProxy(configuration);

        return serviceCollection.BuildServiceProvider();
    }

    private static (ReverseProxyOptions Options, ForwardedHeadersOptions Forwarded) Resolve(
        Dictionary<string, string?> configurationData
    )
    {
        using var serviceProvider = BuildServiceProvider(configurationData);

        return (
            serviceProvider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value,
            serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value
        );
    }
}
