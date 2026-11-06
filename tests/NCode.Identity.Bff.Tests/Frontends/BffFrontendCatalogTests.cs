// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffFrontendCatalogTests : BaseTests
{
    private static BffFrontendOptions CreateOptions(string? rule = null, params string[] values) =>
        BffTestFrontends.CreateOptions(x =>
        {
            if (rule is not null)
                x.Selection = new BffFrontendSelectionOptions { Rule = rule, Values = [.. values] };
        });

    private static HttpContext CreateContext(string host, string path = "/")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.Path = path;
        return httpContext;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Always_DefersReadingTheOptionsToFirstUse()
    {
        var mockOptions = CreateStrictMock<Microsoft.Extensions.Options.IOptions<BffOptions>>();

        var catalog = new BffFrontendCatalog(
            mockOptions.Object,
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

        mockOptions
            .SetupGet(x => x.Value)
            .Returns(
                new BffOptions
                {
                    Frontends = { [BffTestFrontends.Name] = BffTestFrontends.CreateOptions() },
                }
            )
            .Verifiable();

        var frontend = Assert.Single(catalog.Frontends);
        Assert.Equal(BffTestFrontends.Name, frontend.Name);
    }

    #endregion

    #region Frontends Tests

    [Fact]
    public void Frontends_AreOrderedByName()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["shop"] = CreateOptions(BffFrontendSelectorNames.Host, "shop.example.com"),
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["web"] = CreateOptions(),
                },
            }
        );

        Assert.Equal(["admin", "shop", "web"], catalog.Frontends.Select(x => x.Name));
    }

    [Fact]
    public void Frontends_RuleThatIsNotRegistered_Throws()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions { Frontends = { ["web"] = CreateOptions("custom", "x") } }
        );

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.Frontends);

        Assert.Equal(
            "Bff:Frontends:web:Selection:Rule 'custom' is not a registered frontend selection rule.",
            exception.Message
        );
    }

    [Fact]
    public void Frontends_RuleRegisteredTwice_Throws()
    {
        var catalog = new BffFrontendCatalog(
            Microsoft.Extensions.Options.Options.Create(new BffOptions()),
            [new HostBffFrontendSelector(), new HostBffFrontendSelector()]
        );

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.Frontends);

        Assert.Equal(
            "The frontend selection rule 'host' is registered more than once.",
            exception.Message
        );
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_KnownName_ReturnsTheFrontend()
    {
        var catalog = BffTestFrontends.CreateCatalog();

        var found = catalog.TryGet(BffTestFrontends.Name, out var frontend);

        Assert.True(found);
        Assert.Same(BffTestFrontends.Get(catalog), frontend);
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse()
    {
        var catalog = BffTestFrontends.CreateCatalog();

        var found = catalog.TryGet("other", out var frontend);

        Assert.False(found);
        Assert.Null(frontend);
    }

    #endregion

    #region Resolve Tests

    [Fact]
    public void Resolve_OneFallbackFrontend_SelectsAnyRequest()
    {
        var catalog = BffTestFrontends.CreateCatalog();

        var frontend = catalog.Resolve(CreateContext("anything.example.org", "/x/y"));

        Assert.Same(BffTestFrontends.Get(catalog), frontend);
    }

    [Fact]
    public void Resolve_HostRuleMatches_BeatsTheFallback()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["web"] = CreateOptions(),
                },
            }
        );

        var admin = catalog.Resolve(CreateContext("admin.example.com"));
        var other = catalog.Resolve(CreateContext("app.example.com"));

        Assert.Equal("admin", admin?.Name);
        Assert.Equal("web", other?.Name);
    }

    [Fact]
    public void Resolve_NoRuleMatchesAndNoFallback_ReturnsNull()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                },
            }
        );

        var frontend = catalog.Resolve(CreateContext("app.example.com"));

        Assert.Null(frontend);
    }

    [Fact]
    public void Resolve_HostAndPathFrontendsBothMatch_Throws()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["shop"] = CreateOptions(BffFrontendSelectorNames.Path, "/shop"),
                },
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.Resolve(CreateContext("admin.example.com", "/shop/cart"))
        );

        Assert.Equal(
            "The request is selected by the BFF frontends 'admin' and 'shop'; their selection rules must not overlap.",
            exception.Message
        );
    }

    [Fact]
    public void Resolve_CalledAgain_ReturnsTheFirstFrontend()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["web"] = CreateOptions(),
                },
            }
        );
        var httpContext = CreateContext("admin.example.com");
        var first = catalog.Resolve(httpContext);

        httpContext.Request.Host = new HostString("app.example.com");
        var second = catalog.Resolve(httpContext);

        Assert.Equal("admin", first?.Name);
        Assert.Same(first, second);
    }

    #endregion

    #region GetRequired Tests

    [Fact]
    public void GetRequired_FrontendServesTheRequest_ReturnsIt()
    {
        var catalog = BffTestFrontends.CreateCatalog();

        var frontend = catalog.GetRequired(CreateContext("app.example.com"));

        Assert.Same(BffTestFrontends.Get(catalog), frontend);
    }

    [Fact]
    public void GetRequired_NoFrontendServesTheRequest_Throws()
    {
        var catalog = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                },
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.GetRequired(CreateContext("app.example.com"))
        );

        Assert.Equal(
            "No BFF frontend serves this request: a frontend's Bff:Frontends:<name>:Selection must select it, or one frontend must have no selection rule.",
            exception.Message
        );
    }

    #endregion
}
