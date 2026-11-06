// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffFrontendSetTests : BaseTests
{
    private static IBffFrontendSelector[] Selectors =>
        [new HostBffFrontendSelector(), new PathBffFrontendSelector()];

    private static BffFrontendOptions CreateOptions(string? rule = null, params string[] values) =>
        BffTestFrontends.CreateOptions(x =>
        {
            if (rule is not null)
                x.Selection = new BffFrontendSelectionOptions { Rule = rule, Values = [.. values] };
        });

    private static BffFrontendSet CreateSet(BffOptions options) =>
        BffFrontendSet.Create(options, Selectors);

    private static HttpRequest CreateRequest(string host, string path = "/")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.Path = path;
        return httpContext.Request;
    }

    #region Create Tests

    [Fact]
    public void Create_Always_OrdersTheFrontendsByName()
    {
        var set = CreateSet(
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

        Assert.Equal(["admin", "shop", "web"], set.Frontends.Select(x => x.Name));
    }

    [Fact]
    public void Create_RuleThatIsNotRegistered_Throws()
    {
        var options = new BffOptions { Frontends = { ["web"] = CreateOptions("custom", "x") } };

        var exception = Assert.Throws<InvalidOperationException>(() => CreateSet(options));

        Assert.Equal(
            "Bff:Frontends:web:Selection:Rule 'custom' is not a registered frontend selection rule.",
            exception.Message
        );
    }

    [Fact]
    public void Create_RuleRegisteredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BffFrontendSet.Create(
                new BffOptions(),
                [new HostBffFrontendSelector(), new HostBffFrontendSelector()]
            )
        );

        Assert.Equal(
            "The frontend selection rule 'host' is registered more than once.",
            exception.Message
        );
    }

    #endregion

    #region Options Tests

    [Fact]
    public void Options_Always_IsTheGivenOptions()
    {
        var options = new BffOptions { Frontends = { ["web"] = CreateOptions() } };

        var set = CreateSet(options);

        Assert.Same(options, set.Options);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_ConfiguredName_ReturnsTheFrontend()
    {
        var set = CreateSet(
            new BffOptions
            {
                Frontends =
                {
                    ["web"] = CreateOptions(),
                    ["shop"] = CreateOptions(BffFrontendSelectorNames.Host, "shop.example.com"),
                },
            }
        );

        var found = set.TryGet("shop", out var frontend);

        Assert.True(found);
        Assert.Same(set.Frontends.Single(x => x.Name == "shop"), frontend);
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse()
    {
        var set = CreateSet(new BffOptions { Frontends = { ["web"] = CreateOptions() } });

        var found = set.TryGet("other", out var frontend);

        Assert.False(found);
        Assert.Null(frontend);
    }

    #endregion

    #region Select Tests

    [Fact]
    public void Select_NoRuleMatches_ReturnsTheFallback()
    {
        var set = CreateSet(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["web"] = CreateOptions(),
                },
            }
        );

        var frontend = set.Select(CreateRequest("app.example.com"));

        Assert.Equal("web", frontend?.Name);
    }

    [Fact]
    public void Select_RuleMatches_ReturnsThatFrontend()
    {
        var set = CreateSet(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                    ["web"] = CreateOptions(),
                },
            }
        );

        var frontend = set.Select(CreateRequest("admin.example.com"));

        Assert.Equal("admin", frontend?.Name);
    }

    [Fact]
    public void Select_TwoRulesMatch_Throws()
    {
        var set = CreateSet(
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
            set.Select(CreateRequest("admin.example.com", "/shop/cart"))
        );

        Assert.Equal(
            "The request is selected by the BFF frontends 'admin' and 'shop'; their selection rules must not overlap.",
            exception.Message
        );
    }

    [Fact]
    public void Select_NoMatchAndNoFallback_ReturnsNull()
    {
        var set = CreateSet(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = CreateOptions(BffFrontendSelectorNames.Host, "admin.example.com"),
                },
            }
        );

        var frontend = set.Select(CreateRequest("app.example.com"));

        Assert.Null(frontend);
    }

    #endregion
}
