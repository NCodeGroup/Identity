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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveSurfacePathsTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private ServiceProvider ServiceProvider { get; }
    private InteractiveSurfacePaths SurfacePaths { get; }

    public InteractiveSurfacePathsTests()
    {
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        ServiceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        SurfacePaths = new InteractiveSurfacePaths(
            MockTenantResolver.Object,
            ServiceProvider.GetRequiredService<TemplateBinderFactory>()
        );
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        MockRepository.Verify();
    }

    private void SetupTenantRoute(string tenantRoute) =>
        MockTenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse(tenantRoute))
            .Verifiable();

    private static DefaultHttpContext CreateHttpContext(
        string pathBase = "",
        params (string Key, object Value)[] routeValues
    )
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = new PathString(pathBase);
        foreach (var (key, value) in routeValues)
        {
            httpContext.Request.RouteValues[key] = value;
        }

        return httpContext;
    }

    #region GetTenantPath Tests

    [Fact]
    public void GetTenantPath_WhenTenantRouteIsEmpty_ReturnsThePathBase()
    {
        SetupTenantRoute("");

        var tenantPath = SurfacePaths.GetTenantPath(CreateHttpContext("/host"));

        Assert.Equal("/host", tenantPath.Value);
    }

    [Fact]
    public void GetTenantPath_WhenTenantRouteIsStatic_ReturnsTheRoute()
    {
        SetupTenantRoute("/identity");

        var tenantPath = SurfacePaths.GetTenantPath(CreateHttpContext());

        Assert.Equal("/identity", tenantPath.Value);
    }

    [Fact]
    public void GetTenantPath_WhenTenantRouteHasAParameter_BindsItFromTheRequest()
    {
        SetupTenantRoute("/{tenantId}");

        var tenantPath = SurfacePaths.GetTenantPath(
            CreateHttpContext("/host", ("tenantId", "acme"), ("unrelated", "value"))
        );

        Assert.Equal("/host/acme", tenantPath.Value);
    }

    [Fact]
    public void GetTenantPath_WhenTheRequestLacksTheRoutesValues_ReturnsThePathBase()
    {
        SetupTenantRoute("/{tenantId}");

        var tenantPath = SurfacePaths.GetTenantPath(CreateHttpContext("/host"));

        Assert.Equal("/host", tenantPath.Value);
    }

    #endregion

    #region GetSurfacePath Tests

    [Fact]
    public void GetSurfacePath_WhenTenantRouteHasAParameter_ReturnsThePathBeneathTheTenant()
    {
        SetupTenantRoute("/{tenantId}");

        var surfacePath = SurfacePaths.GetSurfacePath(
            CreateHttpContext(routeValues: ("tenantId", "acme")),
            InteractiveAuthenticationDefaults.LoginPath
        );

        Assert.Equal("/acme/account/login", surfacePath);
    }

    [Fact]
    public void GetSurfacePath_WhenTenantRouteIsEmpty_ReturnsTheSurfacePath()
    {
        SetupTenantRoute("");

        var surfacePath = SurfacePaths.GetSurfacePath(
            CreateHttpContext(),
            InteractiveAuthenticationDefaults.LoginPath
        );

        Assert.Equal("/account/login", surfacePath);
    }

    #endregion
}
