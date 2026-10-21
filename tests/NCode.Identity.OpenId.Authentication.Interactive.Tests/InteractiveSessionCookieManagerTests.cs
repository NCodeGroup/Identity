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

public sealed class InteractiveSessionCookieManagerTests : IDisposable
{
    private const string CookieName = "session";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private ServiceProvider ServiceProvider { get; }
    private InteractiveSessionCookieManager CookieManager { get; }

    public InteractiveSessionCookieManagerTests()
    {
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        ServiceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        CookieManager = new InteractiveSessionCookieManager(
            new InteractiveSurfacePaths(
                MockTenantResolver.Object,
                ServiceProvider.GetRequiredService<TemplateBinderFactory>()
            )
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

    private static DefaultHttpContext CreateHttpContext(string? tenantId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (tenantId is not null)
        {
            httpContext.Request.RouteValues["tenantId"] = tenantId;
        }

        return httpContext;
    }

    private static string ReadSetCookie(HttpContext httpContext) =>
        Assert.Single(httpContext.Response.Headers.SetCookie.ToArray())!;

    #region AppendResponseCookie Tests

    [Fact]
    public void AppendResponseCookie_WhenTenantIsServedUnderAPath_ScopesTheCookieToIt()
    {
        SetupTenantRoute("/{tenantId}");
        var httpContext = CreateHttpContext("acme");
        var options = new CookieOptions { Path = "/", HttpOnly = true };

        CookieManager.AppendResponseCookie(httpContext, CookieName, "value", options);

        var setCookie = ReadSetCookie(httpContext);
        Assert.StartsWith($"{CookieName}=value", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/acme", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.Ordinal);
        // The caller's options are copied, not mutated.
        Assert.Equal("/", options.Path);
    }

    [Fact]
    public void AppendResponseCookie_WhenTenantIsServedAtTheRoot_ScopesTheCookieToTheRoot()
    {
        SetupTenantRoute("");
        var httpContext = CreateHttpContext();

        CookieManager.AppendResponseCookie(httpContext, CookieName, "value", new CookieOptions());

        Assert.Contains("path=/", ReadSetCookie(httpContext), StringComparison.Ordinal);
    }

    #endregion

    #region DeleteCookie Tests

    [Fact]
    public void DeleteCookie_WhenTenantIsServedUnderAPath_ExpiresTheCookieAtThatPath()
    {
        SetupTenantRoute("/{tenantId}");
        var httpContext = CreateHttpContext("acme");

        CookieManager.DeleteCookie(httpContext, CookieName, new CookieOptions { Path = "/" });

        var setCookie = ReadSetCookie(httpContext);
        Assert.StartsWith($"{CookieName}=;", setCookie, StringComparison.Ordinal);
        Assert.Contains("expires=", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/acme", setCookie, StringComparison.Ordinal);
    }

    #endregion

    #region GetRequestCookie Tests

    [Fact]
    public void GetRequestCookie_WhenCookieIsPresent_ReturnsItsValue()
    {
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Cookie = $"{CookieName}=value";

        var value = CookieManager.GetRequestCookie(httpContext, CookieName);

        Assert.Equal("value", value);
    }

    #endregion
}
