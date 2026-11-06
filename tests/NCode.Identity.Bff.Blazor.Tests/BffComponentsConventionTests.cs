// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffComponentsConventionTests : BaseTests
{
    private static RouteEndpointBuilder CreateBuilder(params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/test"), 0);
        foreach (var item in metadata)
            builder.Metadata.Add(item);
        return builder;
    }

    #region Apply Tests

    [Fact]
    public void Apply_ComponentPage_AddsPageAndRequirement()
    {
        var builder = CreateBuilder(new ComponentTypeMetadata(typeof(TestPage)));

        BffComponentsConvention.Apply(builder);

        Assert.Single(builder.Metadata.OfType<BffPageAttribute>());
        Assert.Contains(BffBlazorLibraryRequirement.Instance, builder.Metadata);
        Assert.Empty(builder.Metadata.OfType<BffHubAttribute>());
        Assert.Empty(builder.Metadata.OfType<BffSelfRevalidatingHubAttribute>());
    }

    [Fact]
    public void Apply_Hub_AddsHubAnonymousAndRequirement()
    {
        var builder = CreateBuilder(new HubMetadata(typeof(Hub)));

        BffComponentsConvention.Apply(builder);

        Assert.Single(builder.Metadata.OfType<BffHubAttribute>());
        Assert.Single(builder.Metadata.OfType<AllowAnonymousAttribute>());
        Assert.Single(builder.Metadata.OfType<BffSelfRevalidatingHubAttribute>());
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
        Assert.Single(builder.Metadata.OfType<BffSelfRevalidatingHubAttribute>());
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
}
