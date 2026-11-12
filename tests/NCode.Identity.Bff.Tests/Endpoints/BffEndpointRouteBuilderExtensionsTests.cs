// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffEndpointRouteBuilderExtensionsTests : BaseTests
{
    private sealed record Marker;

    private static List<RouteEndpoint> MapEndpoints(Action<BffFrontendOptions>? configure = null) =>
        MapEndpoints(BffTestFrontends.CreateCatalog(configure));

    private static List<RouteEndpoint> MapEndpoints(BffFrontendCatalog catalog)
    {
        var endpoints = MapEndpointRoutes(catalog, out _);

        return ReadEndpoints(endpoints);
    }

    private static IEndpointRouteBuilder MapEndpointRoutes(
        BffFrontendCatalog catalog,
        out RouteGroupBuilder group
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(catalog);

        var app = builder.Build();
        IEndpointRouteBuilder endpoints = app;

        group = endpoints.MapBffEndpoints();

        return endpoints;
    }

    private static List<RouteEndpoint> ReadEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();

    private static BffOptions CreateOptionsWithShop() =>
        BffTestFrontends.CreateBffOptions(x =>
            x.Frontends["shop"] = BffTestFrontends.CreateOptions(y =>
            {
                y.PathPrefix = "/shop/bff";
                y.Selection = new BffFrontendSelectionOptions
                {
                    Rule = BffFrontendSelectorNames.Host,
                    Values = ["shop.example.com"],
                };
            })
        );

    #region MapBffEndpoints Tests

    [Fact]
    public void MapBffEndpoints_MapsTheFiveRoutesUnderThePrefix()
    {
        var endpoints = MapEndpoints();

        var patterns = endpoints.Select(x => x.RoutePattern.RawText).Order().ToList();

        Assert.Equal(
            ["/bff/backchannel-logout", "/bff/callback", "/bff/login", "/bff/logout", "/bff/user"],
            patterns
        );
    }

    [Fact]
    public void MapBffEndpoints_WithCustomPrefix_UsesIt()
    {
        var endpoints = MapEndpoints(x => x.PathPrefix = "/auth");

        var patterns = endpoints.Select(x => x.RoutePattern.RawText).Order().ToList();

        Assert.Equal(
            [
                "/auth/backchannel-logout",
                "/auth/callback",
                "/auth/login",
                "/auth/logout",
                "/auth/user",
            ],
            patterns
        );
    }

    [Fact]
    public void MapBffEndpoints_UserEndpoint_CarriesTheMetadata()
    {
        var endpoints = MapEndpoints();

        var user = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/user");
        var login = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/login");

        Assert.NotNull(user.Metadata.GetMetadata<BffUserEndpointMetadata>());
        Assert.Null(login.Metadata.GetMetadata<BffUserEndpointMetadata>());
    }

    [Fact]
    public void MapBffEndpoints_EveryEndpoint_HasConfidentialBodies()
    {
        var endpoints = MapEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, x => Assert.True(x.HasConfidentialBodies));
    }

    [Fact]
    public void MapBffEndpoints_LogoutEndpoint_AcceptsOnlyPost()
    {
        var endpoints = MapEndpoints();

        var logout = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/logout");

        var methods = logout.Metadata.GetMetadata<HttpMethodMetadata>();
        Assert.NotNull(methods);
        Assert.Equal(["POST"], methods.HttpMethods);
    }

    [Fact]
    public void MapBffEndpoints_WhenJwksEnabled_MapsTheJwksRoute()
    {
        var endpoints = MapEndpoints(x => x.Jwks.Enabled = true);

        var jwks = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/jwks");

        var methods = jwks.Metadata.GetMetadata<HttpMethodMetadata>();
        Assert.NotNull(methods);
        Assert.Equal(["GET"], methods.HttpMethods);
    }

    [Fact]
    public void MapBffEndpoints_WhenJwksDisabled_DoesNotMapTheJwksRoute()
    {
        var endpoints = MapEndpoints();

        Assert.DoesNotContain(endpoints, x => x.RoutePattern.RawText == "/bff/jwks");
    }

    [Fact]
    public void MapBffEndpoints_NamesTheEndpointsAfterTheFrontend()
    {
        var endpoints = MapEndpoints(x => x.Jwks.Enabled = true);

        var names = endpoints
            .Select(x => x.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Order()
            .ToList();

        Assert.Equal(
            [
                "Bff.default.BackchannelLogout",
                "Bff.default.Callback",
                "Bff.default.Jwks",
                "Bff.default.Login",
                "Bff.default.Logout",
                "Bff.default.User",
            ],
            names
        );
    }

    [Fact]
    public void MapBffEndpoints_EveryEndpoint_IsLimitedToItsFrontend()
    {
        var endpoints = MapEndpoints(x => x.Jwks.Enabled = true);

        Assert.All(
            endpoints,
            endpoint =>
            {
                var frontend = Assert.Single(
                    endpoint.Metadata.GetOrderedMetadata<BffFrontendAttribute>()
                );
                Assert.Equal(BffTestFrontends.Name, frontend.Name);
            }
        );
    }

    [Fact]
    public void MapBffEndpoints_TwoFrontends_MapsEachUnderItsPrefix()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["web"] = BffTestFrontends.CreateOptions(x => x.Jwks.Enabled = true),
                    ["admin"] = BffTestFrontends.CreateOptions(x => x.PathPrefix = "/admin/bff"),
                },
            }
        );

        var endpoints = MapEndpoints(catalog);

        var web = endpoints
            .Where(x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "web")
            .Select(x => x.RoutePattern.RawText)
            .Order()
            .ToList();
        var admin = endpoints
            .Where(x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "admin")
            .Select(x => x.RoutePattern.RawText)
            .Order()
            .ToList();

        Assert.Equal(
            [
                "/bff/backchannel-logout",
                "/bff/callback",
                "/bff/jwks",
                "/bff/login",
                "/bff/logout",
                "/bff/user",
            ],
            web
        );
        Assert.Equal(
            [
                "/admin/bff/backchannel-logout",
                "/admin/bff/callback",
                "/admin/bff/login",
                "/admin/bff/logout",
                "/admin/bff/user",
            ],
            admin
        );
    }

    [Fact]
    public void MapBffEndpoints_FrontendAddedByAChange_MapsItsEndpoints()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var endpoints = MapEndpointRoutes(BffTestFrontends.CreateCatalog(source), out _);
        Assert.DoesNotContain(
            ReadEndpoints(endpoints),
            x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "shop"
        );

        source.Change(CreateOptionsWithShop());

        var shop = ReadEndpoints(endpoints)
            .Where(x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "shop")
            .Select(x => x.RoutePattern.RawText)
            .Order()
            .ToList();
        Assert.Equal(
            [
                "/shop/bff/backchannel-logout",
                "/shop/bff/callback",
                "/shop/bff/login",
                "/shop/bff/logout",
                "/shop/bff/user",
            ],
            shop
        );
    }

    [Fact]
    public void MapBffEndpoints_FrontendRemovedByAChange_UnmapsItsEndpoints()
    {
        var source = new BffTestOptionsSource(CreateOptionsWithShop());
        var endpoints = MapEndpointRoutes(BffTestFrontends.CreateCatalog(source), out _);
        Assert.Contains(
            ReadEndpoints(endpoints),
            x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "shop"
        );

        source.Change(BffTestFrontends.CreateBffOptions());

        var remaining = ReadEndpoints(endpoints);
        Assert.NotEmpty(remaining);
        Assert.All(
            remaining,
            x =>
                Assert.Equal(
                    BffTestFrontends.Name,
                    x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name
                )
        );
    }

    [Fact]
    public void MapBffEndpoints_GroupConvention_ReachesAFrontendAddedByAChange()
    {
        var marker = new Marker();
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var endpoints = MapEndpointRoutes(BffTestFrontends.CreateCatalog(source), out var group);
        group.WithMetadata(marker);
        var token = endpoints.DataSources.Single().GetChangeToken();

        source.Change(CreateOptionsWithShop());

        Assert.True(token.HasChanged);
        var shop = ReadEndpoints(endpoints)
            .Where(x => x.Metadata.GetMetadata<BffFrontendAttribute>()?.Name == "shop")
            .ToList();
        Assert.NotEmpty(shop);
        Assert.All(
            ReadEndpoints(endpoints),
            x => Assert.Same(marker, x.Metadata.GetMetadata<Marker>())
        );
    }

    #endregion
}
