// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
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

    private IEndpointRouteBuilder CreateEndpoints(Action<BffFrontendOptions>? configure = null) =>
        CreateEndpoints(BffTestFrontends.CreateCatalog(configure));

    private BffTokenContexts CreateContexts(BffFrontendCatalog catalog)
    {
        var timeProvider = new FakeTimeProvider();
        var cookies = new BffCookies(timeProvider);
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            catalog,
            timeProvider
        );
        return new BffTokenContexts(catalog, resolver, cookies);
    }

    private IEndpointRouteBuilder CreateEndpoints(BffFrontendCatalog catalog)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        var serviceCollection = builder.Services;

        var requirement = CreateLooseMock<IBffTokenRequirement>();
        requirement.SetupGet(x => x.Name).Returns(BffTokenRequirementNames.User);
        serviceCollection.AddSingleton(requirement.Object);
        serviceCollection.AddSingleton(CreateLooseMock<IBffTokenAccessor>().Object);
        serviceCollection.AddSingleton(CreateLooseMock<IDPoPProofFactory>().Object);
        serviceCollection.AddSingleton<
            IBffConnectionSessionAccessor,
            DefaultBffConnectionSessionAccessor
        >();
        serviceCollection.AddSingleton(CreateContexts(catalog));
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddHttpForwarder();
        serviceCollection.AddSingleton(catalog);
        serviceCollection.AddSingleton<BffRemoteApiInvokerFactory>();
        serviceCollection.AddSingleton<BffRemoteApiRegistry>();
        serviceCollection.AddSingleton<BffRemoteApiHandler>();

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

        Assert.Contains(
            "frontend 'default' at '/bff'",
            exception.Message,
            StringComparison.Ordinal
        );
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
    public void MapBffRemoteApis_ConfiguredApi_BelongsToItsFrontend()
    {
        var endpoints = CreateEndpoints(x =>
            x.RemoteApis["orders"] = new BffRemoteApiOptions
            {
                Path = "/api/orders",
                Destination = Destination,
            }
        );

        var group = endpoints.MapBffRemoteApis();

        var endpoint = Assert.Single(GetEndpoints(group));
        Assert.Equal("BFF remote API /api/orders (default)", endpoint.DisplayName);
        Assert.Equal(
            BffTestFrontends.Name,
            Assert.Single(endpoint.Metadata.GetOrderedMetadata<BffFrontendAttribute>()).Name
        );
        Assert.NotNull(endpoint.Metadata.GetMetadata<BffApiAttribute>());
    }

    [Fact]
    public void MapBffRemoteApis_TwoFrontends_MayMapTheSamePath()
    {
        var api = new BffRemoteApiOptions { Path = "/api/orders", Destination = Destination };
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["web"] = BffTestFrontends.CreateOptions(x => x.RemoteApis["orders"] = api),
                    ["admin"] = BffTestFrontends.CreateOptions(x =>
                    {
                        x.PathPrefix = "/admin/bff";
                        x.RemoteApis["orders"] = api;
                    }),
                },
            }
        );
        var endpoints = CreateEndpoints(catalog);

        var group = endpoints.MapBffRemoteApis();

        var names = GetEndpoints(group)
            .Select(x => Assert.Single(x.Metadata.GetOrderedMetadata<BffFrontendAttribute>()).Name)
            .Order()
            .ToList();
        Assert.Equal(["admin", "web"], names);
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
        endpoints.MapBffRemoteApis();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApi("/api", Destination)
        );

        Assert.Contains("Bff:Frontends:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapBffRemoteApis_UnregisteredRequirement_Throws()
    {
        var endpoints = CreateEndpoints(x =>
            x.RemoteApis["orders"] = new BffRemoteApiOptions
            {
                Path = "/api/orders",
                Destination = Destination,
                TokenRequirement = "missing",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            endpoints.MapBffRemoteApis()
        );

        Assert.StartsWith(
            "Bff:Frontends:default:RemoteApis:orders:TokenRequirement 'missing'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void MapBffRemoteApis_FrontendAddedByAChange_MapsItsApis()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var endpoints = CreateEndpoints(BffTestFrontends.CreateCatalog(source));
        endpoints.MapBffRemoteApis();
        Assert.Empty(GetEndpoints(endpoints));

        source.Change(
            BffTestFrontends.CreateBffOptions(x =>
                x.Frontends["shop"] = BffTestFrontends.CreateOptions(y =>
                {
                    y.PathPrefix = "/shop/bff";
                    y.Selection = new BffFrontendSelectionOptions
                    {
                        Rule = BffFrontendSelectorNames.Host,
                        Values = ["shop.example.com"],
                    };
                    y.RemoteApis["orders"] = new BffRemoteApiOptions
                    {
                        Path = "/api/orders",
                        Destination = Destination,
                    };
                })
            )
        );

        var endpoint = Assert.Single(GetEndpoints(endpoints));
        Assert.Equal("BFF remote API /api/orders (shop)", endpoint.DisplayName);
        Assert.Equal(
            "shop",
            Assert.Single(endpoint.Metadata.GetOrderedMetadata<BffFrontendAttribute>()).Name
        );
    }

    [Fact]
    public void MapBffRemoteApi_ChangeAddsAnOverlappingPathPrefix_KeepsTheLastSet()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var catalog = BffTestFrontends.CreateCatalog(source);
        var endpoints = CreateEndpoints(catalog);
        endpoints.MapBffRemoteApi("/api/orders", Destination);

        source.Change(
            BffTestFrontends.CreateBffOptions(x =>
                x.Frontends["shop"] = BffTestFrontends.CreateOptions(y =>
                {
                    y.PathPrefix = "/api";
                    y.Selection = new BffFrontendSelectionOptions
                    {
                        Rule = BffFrontendSelectorNames.Host,
                        Values = ["shop.example.com"],
                    };
                })
            )
        );

        Assert.Equal([BffTestFrontends.Name], catalog.Frontends.Select(x => x.Name));
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
