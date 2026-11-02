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

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Buffers;
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

    private static string[] ReadAttributes(string setCookie) =>
        [.. setCookie.Split(';').Skip(1).Select(x => x.Trim())];

    private static string ExpectedTenantSuffix(string tenantPath) =>
        Base64Url.EncodeToString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(tenantPath)));

    #region AppendResponseCookie Tests

    [Fact]
    public void AppendResponseCookie_WhenTenantIsServedUnderAPath_NamesTheCookieForTheTenant()
    {
        SetupTenantRoute("/{tenantId}");
        var httpContext = CreateHttpContext("acme");
        var options = new CookieOptions
        {
            Path = "/acme",
            Domain = "example.com",
            HttpOnly = true,
        };

        CookieManager.AppendResponseCookie(httpContext, CookieName, "value", options);

        var setCookie = ReadSetCookie(httpContext);
        Assert.StartsWith(
            $"{CookieName}.{ExpectedTenantSuffix("/acme")}=value",
            setCookie,
            StringComparison.Ordinal
        );
        var attributes = ReadAttributes(setCookie);
        Assert.Contains("path=/", attributes);
        Assert.Contains("secure", attributes);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.Ordinal);
        // The caller's options are copied, not mutated.
        Assert.Equal("/acme", options.Path);
        Assert.Equal("example.com", options.Domain);
    }

    [Fact]
    public void AppendResponseCookie_WhenTenantIsServedAtTheRoot_KeepsTheName()
    {
        SetupTenantRoute("");
        var httpContext = CreateHttpContext();

        CookieManager.AppendResponseCookie(httpContext, CookieName, "value", new CookieOptions());

        var setCookie = ReadSetCookie(httpContext);
        Assert.StartsWith($"{CookieName}=value", setCookie, StringComparison.Ordinal);
        var attributes = ReadAttributes(setCookie);
        Assert.Contains("path=/", attributes);
        Assert.Contains("secure", attributes);
    }

    [Fact]
    public void AppendResponseCookie_WhenTwoTenantsShareAnOrigin_GivesEachItsOwnName()
    {
        SetupTenantRoute("/{tenantId}");
        var acmeContext = CreateHttpContext("acme");
        var betaContext = CreateHttpContext("beta");

        CookieManager.AppendResponseCookie(acmeContext, CookieName, "value", new CookieOptions());
        CookieManager.AppendResponseCookie(betaContext, CookieName, "value", new CookieOptions());

        var acmeName = ReadSetCookie(acmeContext).Split('=')[0];
        var betaName = ReadSetCookie(betaContext).Split('=')[0];
        Assert.NotEqual(acmeName, betaName);
    }

    #endregion

    #region DeleteCookie Tests

    [Fact]
    public void DeleteCookie_WhenTenantIsServedUnderAPath_ExpiresTheTenantsCookieAtTheRoot()
    {
        SetupTenantRoute("/{tenantId}");
        var httpContext = CreateHttpContext("acme");

        CookieManager.DeleteCookie(httpContext, CookieName, new CookieOptions { Path = "/acme" });

        var setCookie = ReadSetCookie(httpContext);
        Assert.StartsWith(
            $"{CookieName}.{ExpectedTenantSuffix("/acme")}=;",
            setCookie,
            StringComparison.Ordinal
        );
        Assert.Contains("expires=", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", ReadAttributes(setCookie));
    }

    #endregion

    #region GetRequestCookie Tests

    [Fact]
    public void GetRequestCookie_WhenTenantIsServedUnderAPath_ReadsOnlyThatTenantsCookie()
    {
        SetupTenantRoute("/{tenantId}");
        var httpContext = CreateHttpContext("acme");
        httpContext.Request.Headers.Cookie =
            $"{CookieName}=root; {CookieName}.{ExpectedTenantSuffix("/acme")}=acme";

        var value = CookieManager.GetRequestCookie(httpContext, CookieName);

        Assert.Equal("acme", value);
    }

    [Fact]
    public void GetRequestCookie_WhenTenantIsServedAtTheRoot_ReadsTheBaseName()
    {
        SetupTenantRoute("");
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.Cookie =
            $"{CookieName}=root; {CookieName}.{ExpectedTenantSuffix("/acme")}=acme";

        var value = CookieManager.GetRequestCookie(httpContext, CookieName);

        Assert.Equal("root", value);
    }

    #endregion
}
