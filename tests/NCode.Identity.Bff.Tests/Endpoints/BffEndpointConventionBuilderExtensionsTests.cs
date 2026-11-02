// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Bff.Endpoints;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffEndpointConventionBuilderExtensionsTests : BaseTests
{
    #region AsBffApi Tests

    [Fact]
    public void AsBffApi_OnMinimalApi_AddsTheAttribute()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapGet("/api/x", () => "x").AsBffApi();
        app.MapGet("/api/y", () => "y");

        IEndpointRouteBuilder endpoints = app;
        var routes = endpoints
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var marked = Assert.Single(routes, x => x.RoutePattern.RawText == "/api/x");
        var unmarked = Assert.Single(routes, x => x.RoutePattern.RawText == "/api/y");

        var attribute = Assert.IsType<BffApiAttribute>(
            marked.Metadata.GetMetadata<BffApiAttribute>()
        );
        var authorizeData = Assert.IsAssignableFrom<IAuthorizeData>(attribute);
        Assert.Equal(BffDefaults.AuthenticationScheme, authorizeData.AuthenticationSchemes);
        Assert.Null(unmarked.Metadata.GetMetadata<BffApiAttribute>());
    }

    #endregion

    #region AsBffPage Tests

    [Fact]
    public void AsBffPage_OnMinimalApi_AddsTheAttribute()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapGet("/page/x", () => "x").AsBffPage();
        app.MapGet("/page/y", () => "y");

        IEndpointRouteBuilder endpoints = app;
        var routes = endpoints
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var marked = Assert.Single(routes, x => x.RoutePattern.RawText == "/page/x");
        var unmarked = Assert.Single(routes, x => x.RoutePattern.RawText == "/page/y");

        var attribute = Assert.IsType<BffPageAttribute>(
            marked.Metadata.GetMetadata<BffPageAttribute>()
        );
        var authorizeData = Assert.IsAssignableFrom<IAuthorizeData>(attribute);
        Assert.Equal(BffDefaults.AuthenticationScheme, authorizeData.AuthenticationSchemes);
        Assert.Null(unmarked.Metadata.GetMetadata<BffPageAttribute>());
    }

    #endregion
}
