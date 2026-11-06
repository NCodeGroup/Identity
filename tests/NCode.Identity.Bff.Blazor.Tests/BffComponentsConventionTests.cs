// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffComponentsConventionTests : BaseTests
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

    private static RouteEndpointBuilder CreateBuilder(params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/test"), 0);
        foreach (var item in metadata)
            builder.Metadata.Add(item);
        return builder;
    }

    #region Apply

    [Fact]
    public void Apply_ComponentPage_AddsPageAndRequirement()
    {
        var builder = CreateBuilder(new ComponentTypeMetadata(typeof(TestPage)));

        BffComponentsConvention.Apply(builder);

        Assert.Single(builder.Metadata.OfType<BffPageAttribute>());
        Assert.Contains(BffBlazorLibraryRequirement.Instance, builder.Metadata);
        Assert.Empty(builder.Metadata.OfType<BffHubAttribute>());
    }

    [Fact]
    public void Apply_Hub_AddsHubAnonymousAndRequirement()
    {
        var builder = CreateBuilder(new HubMetadata(typeof(Hub)));

        BffComponentsConvention.Apply(builder);

        Assert.Single(builder.Metadata.OfType<BffHubAttribute>());
        Assert.Single(builder.Metadata.OfType<AllowAnonymousAttribute>());
        Assert.Contains(BffBlazorLibraryRequirement.Instance, builder.Metadata);
        Assert.Empty(builder.Metadata.OfType<BffPageAttribute>());
    }

    [Fact]
    public void Apply_Negotiate_AddsHubAnonymousAndRequirement()
    {
        var builder = CreateBuilder(new NegotiateMetadata());

        BffComponentsConvention.Apply(builder);

        Assert.Single(builder.Metadata.OfType<BffHubAttribute>());
        Assert.Single(builder.Metadata.OfType<AllowAnonymousAttribute>());
        Assert.Contains(BffBlazorLibraryRequirement.Instance, builder.Metadata);
    }

    [Fact]
    public void Apply_OtherEndpoint_IsUntouched()
    {
        var builder = CreateBuilder();

        BffComponentsConvention.Apply(builder);

        Assert.Empty(builder.Metadata);
    }

    #endregion

    #region BffBlazorLibraryRequirement

    [Fact]
    public void Validate_WithoutMarker_ReturnsFailure()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        var failure = BffBlazorLibraryRequirement.Instance.Validate(services);

        Assert.Equal("AsBffComponents() requires AddBffBlazorLibrary().", failure);
    }

    [Fact]
    public void Validate_WithMarker_ReturnsNull()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IRegistrationMarker<BffBlazorLibrary>>(
            default(BffBlazorLibrary)
        );
        using var services = serviceCollection.BuildServiceProvider();

        var failure = BffBlazorLibraryRequirement.Instance.Validate(services);

        Assert.Null(failure);
    }

    #endregion

    #region AsBffComponents

    [Fact]
    public void AsBffComponents_MarksPagesAndHub()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production }
        );
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        var app = builder.Build();
        Apps.Add(app);

        app.MapRazorComponents<TestApp>().AddInteractiveServerRenderMode().AsBffComponents();

        var endpoints = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(x => x.Endpoints)
            .ToList();

        var page = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            x => x.RoutePattern.RawText == "/test"
        );
        Assert.NotNull(page.Metadata.GetMetadata<BffPageAttribute>());
        Assert.NotNull(page.Metadata.GetMetadata<BffBlazorLibraryRequirement>());

        Assert.Contains(
            endpoints,
            x =>
                x.Metadata.GetMetadata<HubMetadata>() is not null
                && x.Metadata.GetMetadata<BffHubAttribute>() is not null
                && x.Metadata.GetMetadata<BffBlazorLibraryRequirement>() is not null
        );

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
