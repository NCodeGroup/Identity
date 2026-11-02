// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffEndpointRouteBuilderExtensionsTests : BaseTests
{
    private static List<RouteEndpoint> MapEndpoints(Action<BffOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        var optionsBuilder = builder.Services.AddOptions<BffOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

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

    #endregion
}
