// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffFrontendEndpointDataSourceTests : BaseTests
{
    private sealed record Marker;

    private static BffOptions CreateOptionsWithShop() =>
        BffTestFrontends.CreateBffOptions(x =>
            x.Frontends["shop"] = BffTestFrontends.CreateOptions(y =>
                y.Selection = new BffFrontendSelectionOptions
                {
                    Rule = BffFrontendSelectorNames.Host,
                    Values = ["shop.example.com"],
                }
            )
        );

    private static void MapFrontend(IEndpointRouteBuilder builder, BffFrontend frontend) =>
        builder.MapGet("/" + frontend.Name, () => frontend.Name);

    private static BffFrontendEndpointDataSource CreateDataSource(BffFrontendCatalog catalog) =>
        new(catalog, new ServiceCollection().BuildServiceProvider(), MapFrontend);

    private static List<string?> Patterns(IEnumerable<Endpoint> endpoints) =>
        [.. endpoints.OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText).Order()];

    #region Endpoints Tests

    [Fact]
    public void Endpoints_Always_MapsEachFrontend()
    {
        var source = new BffTestOptionsSource(CreateOptionsWithShop());
        var dataSource = CreateDataSource(BffTestFrontends.CreateCatalog(source));

        var patterns = Patterns(dataSource.Endpoints);

        Assert.Equal(["/default", "/shop"], patterns);
    }

    [Fact]
    public void Endpoints_SameSet_ReturnsTheSameList()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var dataSource = CreateDataSource(BffTestFrontends.CreateCatalog(source));

        var first = dataSource.Endpoints;
        var second = dataSource.Endpoints;

        Assert.Same(first, second);
    }

    [Fact]
    public void Endpoints_AfterAChange_MapsTheChangedSet()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var dataSource = CreateDataSource(BffTestFrontends.CreateCatalog(source));
        var before = dataSource.Endpoints;

        source.Change(CreateOptionsWithShop());

        var after = dataSource.Endpoints;
        Assert.NotSame(before, after);
        Assert.Equal(["/default", "/shop"], Patterns(after));
    }

    [Fact]
    public void Endpoints_InARouteGroup_GroupConventionsReachAFrontendAddedByAChange()
    {
        var marker = new Marker();
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var catalog = BffTestFrontends.CreateCatalog(source);

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        IEndpointRouteBuilder routes = app;

        var group = app.MapGroup(string.Empty).WithMetadata(marker);
        ((IEndpointRouteBuilder)group).DataSources.Add(
            new BffFrontendEndpointDataSource(catalog, app.Services, MapFrontend)
        );
        var token = routes.DataSources.Single().GetChangeToken();

        source.Change(CreateOptionsWithShop());

        Assert.True(token.HasChanged);
        var endpoints = routes.DataSources.SelectMany(x => x.Endpoints).ToList();
        Assert.Equal(["/default", "/shop"], Patterns(endpoints));
        Assert.All(endpoints, x => Assert.Same(marker, x.Metadata.GetMetadata<Marker>()));
    }

    #endregion

    #region GetChangeToken Tests

    [Fact]
    public void GetChangeToken_AfterAChange_HasChanged()
    {
        var source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        var dataSource = CreateDataSource(BffTestFrontends.CreateCatalog(source));
        _ = dataSource.Endpoints;
        var token = dataSource.GetChangeToken();
        Assert.False(token.HasChanged);

        source.Change(CreateOptionsWithShop());

        Assert.True(token.HasChanged);
    }

    #endregion
}
