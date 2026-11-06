// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffFrontendMatcherPolicyTests : BaseTests
{
    private static BffFrontendMatcherPolicy CreatePolicy(BffFrontendCatalog catalog) =>
        new(catalog);

    private static BffFrontendCatalog CreateCatalog() =>
        BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["web"] = BffTestFrontends.CreateOptions(),
                    ["shop"] = BffTestFrontends.CreateOptions(x =>
                    {
                        x.PublicOrigin = "https://shop.example.com";
                        x.Selection = new BffFrontendSelectionOptions
                        {
                            Rule = BffFrontendSelectorNames.Host,
                            Values = ["shop.example.com"],
                        };
                    }),
                },
            }
        );

    private static Endpoint CreateEndpoint(string name, params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), name);

    private static CandidateSet CreateCandidates(params Endpoint[] endpoints) =>
        new(
            endpoints,
            [.. endpoints.Select(_ => new RouteValueDictionary())],
            [.. endpoints.Select(_ => 0)]
        );

    private static HttpContext CreateContext(string host)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        return httpContext;
    }

    #region AppliesToEndpoints Tests

    [Fact]
    public void AppliesToEndpoints_AnEndpointHasFrontendMetadata_ReturnsTrue()
    {
        var policy = CreatePolicy(CreateCatalog());

        var applies = policy.AppliesToEndpoints([
            CreateEndpoint("plain"),
            CreateEndpoint("web", new BffFrontendAttribute("web")),
        ]);

        Assert.True(applies);
    }

    [Fact]
    public void AppliesToEndpoints_NoEndpointHasFrontendMetadata_ReturnsFalse()
    {
        var policy = CreatePolicy(CreateCatalog());

        var applies = policy.AppliesToEndpoints([CreateEndpoint("plain")]);

        Assert.False(applies);
    }

    #endregion

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_TheRequestSelectsTheFallback_KeepsOnlyItsEndpoint()
    {
        var policy = CreatePolicy(CreateCatalog());
        var candidates = CreateCandidates(
            CreateEndpoint("web", new BffFrontendAttribute("web")),
            CreateEndpoint("shop", new BffFrontendAttribute("shop"))
        );

        await policy.ApplyAsync(CreateContext("app.example.com"), candidates);

        Assert.True(candidates.IsValidCandidate(0));
        Assert.False(candidates.IsValidCandidate(1));
    }

    [Fact]
    public async Task ApplyAsync_TheRequestSelectsAHostFrontend_KeepsOnlyItsEndpoint()
    {
        var policy = CreatePolicy(CreateCatalog());
        var candidates = CreateCandidates(
            CreateEndpoint("web", new BffFrontendAttribute("web")),
            CreateEndpoint("shop", new BffFrontendAttribute("shop"))
        );

        await policy.ApplyAsync(CreateContext("shop.example.com"), candidates);

        Assert.False(candidates.IsValidCandidate(0));
        Assert.True(candidates.IsValidCandidate(1));
    }

    [Fact]
    public async Task ApplyAsync_AnEndpointWithoutFrontendMetadata_StaysValid()
    {
        var policy = CreatePolicy(CreateCatalog());
        var candidates = CreateCandidates(
            CreateEndpoint("plain"),
            CreateEndpoint("shop", new BffFrontendAttribute("shop"))
        );

        await policy.ApplyAsync(CreateContext("app.example.com"), candidates);

        Assert.True(candidates.IsValidCandidate(0));
        Assert.False(candidates.IsValidCandidate(1));
    }

    [Fact]
    public async Task ApplyAsync_AnEndpointNamingBothFrontends_StaysValid()
    {
        var policy = CreatePolicy(CreateCatalog());
        var candidates = CreateCandidates(
            CreateEndpoint(
                "both",
                new BffFrontendAttribute("web"),
                new BffFrontendAttribute("shop")
            )
        );

        await policy.ApplyAsync(CreateContext("shop.example.com"), candidates);

        Assert.True(candidates.IsValidCandidate(0));
    }

    [Fact]
    public async Task ApplyAsync_NoFrontendServesTheRequest_DropsEveryFrontendEndpoint()
    {
        var catalog = BffTestFrontends.CreateCatalog(x =>
            x.Selection = new BffFrontendSelectionOptions
            {
                Rule = BffFrontendSelectorNames.Host,
                Values = ["app.example.com"],
            }
        );
        var policy = CreatePolicy(catalog);
        var candidates = CreateCandidates(
            CreateEndpoint("default", new BffFrontendAttribute(BffTestFrontends.Name)),
            CreateEndpoint("plain")
        );

        await policy.ApplyAsync(CreateContext("other.example.com"), candidates);

        Assert.False(candidates.IsValidCandidate(0));
        Assert.True(candidates.IsValidCandidate(1));
    }

    #endregion
}
