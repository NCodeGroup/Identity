// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffRazorComponentsEndpointConventionBuilderExtensionsTests : BaseTests
{
    private List<WebApplication> Apps { get; } = [];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var app in Apps)
                ((IDisposable)app).Dispose();
        }

        base.Dispose(disposing);
    }

    private List<Endpoint> MapComponentEndpoints()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        var app = builder.Build();
        Apps.Add(app);

        app.MapRazorComponents<TestApp>().AddInteractiveServerRenderMode().AsBffComponents();

        return ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).ToList();
    }

    #region AsBffComponents Tests

    [Fact]
    public void AsBffComponents_ComponentPage_IsBffPageWithRequirement()
    {
        var endpoints = MapComponentEndpoints();

        var page = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            x => x.RoutePattern.RawText == "/test"
        );

        Assert.NotNull(page.Metadata.GetMetadata<BffPageAttribute>());
        Assert.NotNull(page.Metadata.GetMetadata<BffBlazorLibraryRequirement>());
    }

    [Fact]
    public void AsBffComponents_BlazorHub_IsBffHubWithRequirement()
    {
        var endpoints = MapComponentEndpoints();

        Assert.Contains(
            endpoints,
            x =>
                x.Metadata.GetMetadata<HubMetadata>() is not null
                && x.Metadata.GetMetadata<BffHubAttribute>() is not null
                && x.Metadata.GetMetadata<BffBlazorLibraryRequirement>() is not null
        );
    }

    [Fact]
    public void AsBffComponents_FrameworkEndpoint_IsUntouched()
    {
        var endpoints = MapComponentEndpoints();

        Assert.Contains(
            endpoints,
            x =>
                x.Metadata.GetMetadata<BffPageAttribute>() is null
                && x.Metadata.GetMetadata<BffHubAttribute>() is null
                && x.Metadata.GetMetadata<BffBlazorLibraryRequirement>() is null
        );
    }

    #endregion
}
