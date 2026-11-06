// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public sealed class BffRemoteApiEndpointsTests : BaseTests
{
    private const string Destination = "https://api.example.com/v1";

    private WebApplication? App { get; set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing && App is not null)
            ((IDisposable)App).Dispose();
        base.Dispose(disposing);
    }

    private IEndpointRouteBuilder CreateEndpoints(Action<BffOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        var services = builder.Services;
        var optionsBuilder = services.AddOptions<BffOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        var requirement = CreateLooseMock<IBffTokenRequirement>();
        requirement.SetupGet(x => x.Name).Returns(BffTokenRequirementNames.User);
        services.AddSingleton(requirement.Object);
        services.AddSingleton(CreateLooseMock<IBffTokenAccessor>().Object);
        services.AddSingleton(CreateLooseMock<IDPoPProofFactory>().Object);
        services.AddHttpContextAccessor();
        services.AddHttpForwarder();
        services.AddSingleton(BffTestFrontends.CreateCatalog());
        services.AddSingleton<BffRemoteApiInvokerFactory>();
        services.AddSingleton<BffRemoteApiRegistry>();
        services.AddSingleton<BffRemoteApiHandler>();

        App = builder.Build();
        return App;
    }

    private static List<RouteEndpoint> GetEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();

    #region MapBffRemoteApi Tests

    [Fact]
    public void MapBffRemoteApi_MapsACatchAllRouteMarkedAsABffApi()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapBffRemoteApi("/api/orders", Destination);

        var endpoint = Assert.Single(GetEndpoints(endpoints));
        Assert.Equal("/api/orders/{**remainder}", endpoint.RoutePattern.RawText);
        Assert.Equal("BFF remote API /api/orders", endpoint.DisplayName);
        Assert.NotNull(endpoint.Metadata.GetMetadata<BffApiAttribute>());
    }

    [Fact]
    public void MapBffRemoteApi_InARouteGroup_KeepsTheGroupPrefix()
    {
        var endpoints = CreateEndpoints();
        var group = endpoints.MapGroup("/tenant");

        group.MapBffRemoteApi("/api/orders", Destination);

        var endpoint = Assert.Single(GetEndpoints(endpoints));
        Assert.Equal("/tenant/api/orders/{**remainder}", endpoint.RoutePattern.RawText);
    }

    [Fact]
    public void MapBffRemoteApi_AppliesTheConfiguration()
    {
        var endpoints = CreateEndpoints();
        BffRemoteApiOptions? configured = null;

        endpoints.MapBffRemoteApi(
            "/api/orders",
            Destination,
            api =>
            {
                api.AllowedMethods = ["GET"];
                configured = api;
            }
        );

        Assert.NotNull(configured);
        Assert.Equal("/api/orders", configured.Path);
        Assert.Equal(Destination, configured.Destination);
    }

    [Fact]
    public void MapBffRemoteApi_InvalidDestination_Throws()
    {
        var endpoints = CreateEndpoints();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApi("/api/orders", "http://api.example.com")
        );

        Assert.StartsWith(
            "MapBffRemoteApi:Destination",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void MapBffRemoteApi_OverlapsThePathPrefix_Throws()
    {
        var endpoints = CreateEndpoints();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApi("/bff/orders", Destination)
        );

        Assert.Contains("Bff:PathPrefix '/bff'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBffRemoteApi_EmptyPathPrefix_SkipsThePrefixCheck()
    {
        var endpoints = CreateEndpoints(x => x.PathPrefix = string.Empty);

        endpoints.MapBffRemoteApi("/bff/orders", Destination);

        Assert.Single(GetEndpoints(endpoints));
    }

    [Fact]
    public void MapBffRemoteApi_UnknownTokenRequirement_Throws()
    {
        var endpoints = CreateEndpoints();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApi(
                "/api/orders",
                Destination,
                api => api.TokenRequirement = "missing"
            )
        );

        Assert.Contains("'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBffRemoteApi_OverlappingPath_Throws()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapBffRemoteApi("/api", Destination);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApi("/api/orders", Destination)
        );

        Assert.Contains("mapped at '/api'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBffRemoteApi_NullArguments_Throw()
    {
        var endpoints = CreateEndpoints();

        Assert.Throws<ArgumentNullException>(() =>
            ((IEndpointRouteBuilder)null!).MapBffRemoteApi("/api", Destination)
        );
        Assert.Throws<ArgumentNullException>(() => endpoints.MapBffRemoteApi(null!, Destination));
        Assert.Throws<ArgumentNullException>(() => endpoints.MapBffRemoteApi("/api", null!));
    }

    #endregion

    #region MapBffRemoteApis Tests

    [Fact]
    public void MapBffRemoteApis_MapsEachConfiguredApi()
    {
        var endpoints = CreateEndpoints(x =>
        {
            x.RemoteApis["orders"] = new BffRemoteApiOptions
            {
                Path = "/api/orders",
                Destination = Destination,
            };
            x.RemoteApis["customers"] = new BffRemoteApiOptions
            {
                Path = "/api/customers",
                Destination = Destination,
            };
        });

        var group = endpoints.MapBffRemoteApis();

        var patterns = GetEndpoints(group).Select(x => x.RoutePattern.RawText).Order().ToList();
        Assert.Equal(["/api/customers/{**remainder}", "/api/orders/{**remainder}"], patterns);
    }

    [Fact]
    public void MapBffRemoteApis_OverlapsACodeMappedApi_ThrowsWithTheConfigurationKey()
    {
        var endpoints = CreateEndpoints(x =>
            x.RemoteApis["orders"] = new BffRemoteApiOptions
            {
                Path = "/api/orders",
                Destination = Destination,
            }
        );
        endpoints.MapBffRemoteApi("/api", Destination);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApis()
        );

        Assert.StartsWith(
            "Bff:RemoteApis:orders:Path",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void MapBffRemoteApis_NullEndpoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEndpointRouteBuilder)null!).MapBffRemoteApis()
        );
    }

    #endregion
}
