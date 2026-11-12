// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public class HttpBodyLoggingEndpointConventionBuilderExtensionsTests : BaseTests
{
    private static IEndpointRouteBuilder CreateEndpointRouteBuilder() =>
        WebApplication.CreateSlimBuilder().Build();

    private static List<RouteEndpoint> ReadEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();

    #region WithRequestBodyLogging Tests

    [Fact]
    public void WithRequestBodyLogging_Always_OptsInToTheRequestBody()
    {
        var endpoints = CreateEndpointRouteBuilder();
        endpoints.MapPost("/a", () => Results.Ok()).WithRequestBodyLogging();

        var endpoint = Assert.Single(ReadEndpoints(endpoints));

        Assert.Equal(HttpLoggingFields.RequestBody, endpoint.LoggableBodyFields);
    }

    [Fact]
    public void WithRequestBodyLogging_WithResponseBodyLogging_OptsInToBothBodies()
    {
        var endpoints = CreateEndpointRouteBuilder();
        endpoints
            .MapPost("/a", () => Results.Ok())
            .WithRequestBodyLogging()
            .WithResponseBodyLogging();

        var endpoint = Assert.Single(ReadEndpoints(endpoints));

        Assert.Equal(
            HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody,
            endpoint.LoggableBodyFields
        );
    }

    [Fact]
    public void WithRequestBodyLogging_UnderAGroupWithConfidentialBodies_ThrowsWhenTheEndpointIsBuilt()
    {
        var endpoints = CreateEndpointRouteBuilder();
        endpoints
            .MapGroup("/g")
            .WithConfidentialBodies()
            .MapPost("/a", () => Results.Ok())
            .WithRequestBodyLogging();

        var exception = Assert.Throws<InvalidOperationException>(() => ReadEndpoints(endpoints));

        Assert.Contains("confidential bodies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithRequestBodyLogging_WhenTheBuilderIsNull_ThrowsArgumentNullException()
    {
        IEndpointConventionBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.WithRequestBodyLogging()
        );

        Assert.Equal("builder", exception.ParamName);
    }

    #endregion

    #region WithResponseBodyLogging Tests

    [Fact]
    public void WithResponseBodyLogging_Always_OptsInToTheResponseBody()
    {
        var endpoints = CreateEndpointRouteBuilder();
        endpoints.MapPost("/a", () => Results.Ok()).WithResponseBodyLogging();

        var endpoint = Assert.Single(ReadEndpoints(endpoints));

        Assert.Equal(HttpLoggingFields.ResponseBody, endpoint.LoggableBodyFields);
    }

    [Fact]
    public void WithResponseBodyLogging_OnAnEndpointWithConfidentialBodies_ThrowsWhenTheEndpointIsBuilt()
    {
        var endpoints = CreateEndpointRouteBuilder();
        endpoints
            .MapPost("/a", () => Results.Ok())
            .WithConfidentialBodies()
            .WithResponseBodyLogging();

        var exception = Assert.Throws<InvalidOperationException>(() => ReadEndpoints(endpoints));

        Assert.Contains("confidential bodies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithResponseBodyLogging_WhenTheBuilderIsNull_ThrowsArgumentNullException()
    {
        IEndpointConventionBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.WithResponseBodyLogging()
        );

        Assert.Equal("builder", exception.ParamName);
    }

    #endregion

    #region WithConfidentialBodies Tests

    [Fact]
    public void WithConfidentialBodies_OnAGroup_MarksEveryEndpoint()
    {
        var endpoints = CreateEndpointRouteBuilder();
        var group = endpoints.MapGroup("/g").WithConfidentialBodies();
        group.MapPost("/a", () => Results.Ok());
        group.MapPost("/b", () => Results.Ok());

        var routeEndpoints = ReadEndpoints(endpoints);

        Assert.Equal(2, routeEndpoints.Count);
        Assert.All(routeEndpoints, x => Assert.True(x.HasConfidentialBodies));
    }

    [Fact]
    public void WithConfidentialBodies_WhenTheBuilderIsNull_ThrowsArgumentNullException()
    {
        IEndpointConventionBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.WithConfidentialBodies()
        );

        Assert.Equal("builder", exception.ParamName);
    }

    #endregion
}
