// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Server;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that a backend-for-frontend (BFF) host refuses to start when the BFF metadata of a YARP route is incomplete.
/// </summary>
public class BffEndpointStartupCheckEndToEndTests
{
    private const string RouteId = "orders";

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenATokenRouteIsNotMappedAsARemoteApi_ThrowsNamingTheRouteAndTheConvention()
    {
        await using var app = CreateApp(addTransforms: true, asRemoteApis: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        Assert.Contains($"'{RouteId}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AsBffRemoteApis()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenATokenRouteIsMappedAsARemoteApi_Starts()
    {
        await using var app = CreateApp(addTransforms: true, asRemoteApis: true);

        await app.StartAsync();

        await app.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTheBffTransformsAreNotAdded_ThrowsNamingTheTransforms()
    {
        await using var app = CreateApp(addTransforms: false, asRemoteApis: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());

        Assert.Contains($"'{RouteId}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddBffTransforms()", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Scaffolding

    private static WebApplication CreateApp(bool addTransforms, bool asRemoteApis)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            }
        );
        builder.WebHost.UseTestServer();

        const string web = "Bff:Frontends:web";
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{web}:Authority"] = "https://op.example",
                [$"{web}:PublicOrigin"] = "https://bff.example",
                [$"{web}:ClientId"] = "bff",
                [$"{web}:ReturnUrls:0"] = "/",
            }
        );

        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var services = builder.Services;
        services.AddSingleton(keySet);
        services.AddSingleton(valueSet);
        services.AddSingleton(indexSet);
        services.AddIdentityBff(builder.Configuration);

        var proxy = services
            .AddReverseProxy()
            .LoadFromMemory(
                [
                    new RouteConfig
                    {
                        RouteId = RouteId,
                        ClusterId = "orders-cluster",
                        Match = new RouteMatch { Path = "/api/orders/{**rest}" },
                        Metadata = new Dictionary<string, string>
                        {
                            [BffReverseProxyMetadataKeys.TokenRequirement] =
                                BffTokenRequirementNames.User,
                        },
                    },
                ],
                [
                    new ClusterConfig
                    {
                        ClusterId = "orders-cluster",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["one"] = new() { Address = "https://orders.example/" },
                        },
                    },
                ]
            );
        if (addTransforms)
            proxy.AddBffTransforms();

        var app = builder.Build();
        var routes = app.MapReverseProxy();
        if (asRemoteApis)
            routes.AsBffRemoteApis();

        return app;
    }

    #endregion
}
