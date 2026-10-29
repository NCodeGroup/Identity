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
