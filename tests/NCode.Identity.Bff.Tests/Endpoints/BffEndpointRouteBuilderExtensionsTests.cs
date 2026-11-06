// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffEndpointRouteBuilderExtensionsTests : BaseTests
{
    private static List<RouteEndpoint> MapEndpoints(Action<BffFrontendOptions>? configure = null) =>
        MapEndpoints(BffTestFrontends.CreateCatalog(configure));

    private static List<RouteEndpoint> MapEndpoints(BffFrontendCatalog catalog)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(catalog);

        var app = builder.Build();
        IEndpointRouteBuilder endpoints = app;

        endpoints.MapBffEndpoints();

        return endpoints.DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
    }

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

    #endregion
}
