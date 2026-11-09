// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffTokenContextsTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider TimeProvider { get; } = new(Now);

    private Mock<BffSessionResolver> CreateResolver(BffFrontendCatalog catalog) =>
        CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(TimeProvider),
            Array.Empty<IBffSessionValidator>(),
            catalog,
            TimeProvider
        );

    #region ForRequest Tests

    [Fact]
    public void ForRequest_ReturnsRequestContext()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var contexts = new BffTokenContexts(catalog, resolver.Object, new BffCookies(TimeProvider));
        var httpContext = new DefaultHttpContext();

        var scope = contexts.ForRequest(httpContext);

        Assert.Same(httpContext, scope.HttpContext);
        Assert.Equal(BffTestFrontends.Name, scope.Frontend);
    }

    [Fact]
    public void ForRequest_HubWithConnection_ReturnsConnection()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var contexts = new BffTokenContexts(catalog, resolver.Object, new BffCookies(TimeProvider));
        var connection = new DefaultBffConnectionSession(
            BffTestFrontends.Name,
            new string('a', 43),
            "/bff/logout",
            resolver.Object,
            catalog
        );
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffHubAttribute()), "hub")
        );
        httpContext.User = new ClaimsPrincipal(
            new BffConnectionIdentity(new ClaimsIdentity("test"), connection)
        );

        var scope = contexts.ForRequest(httpContext);

        Assert.Same(connection, scope);
    }

    [Fact]
    public void ForRequest_HubWithoutConnection_ReturnsRequestContext()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var contexts = new BffTokenContexts(catalog, resolver.Object, new BffCookies(TimeProvider));
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(null, new EndpointMetadataCollection(new BffHubAttribute()), "hub")
        );

        var scope = contexts.ForRequest(httpContext);

        Assert.Same(httpContext, scope.HttpContext);
    }

    #endregion
}
