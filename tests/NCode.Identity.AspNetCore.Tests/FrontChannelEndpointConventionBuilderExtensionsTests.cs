// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public class FrontChannelEndpointConventionBuilderExtensionsTests : BaseTests
{
    private static RouteEndpointBuilder CreateEndpointBuilder(RequestDelegate requestDelegate)
    {
        var endpointBuilder = new RouteEndpointBuilder(
            requestDelegate,
            RoutePatternFactory.Parse("/"),
            order: 0
        );
        endpointBuilder.Metadata.Add(new HttpMethodMetadata([HttpMethods.Get, HttpMethods.Post]));
        return endpointBuilder;
    }

    private static Endpoint BuildFrontChannelEndpoint(RouteEndpointBuilder endpointBuilder)
    {
        var conventionBuilder = new RecordingConventionBuilder();
        var result = conventionBuilder.WithFrontChannel();
        Assert.Same(conventionBuilder, result);

        conventionBuilder.Apply(endpointBuilder);
        return endpointBuilder.Build();
    }

    private static HttpContext CreatePreflightContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Options;
        httpContext.Request.Headers[HeaderNames.AccessControlRequestMethod] = HttpMethods.Post;
        return httpContext;
    }

    #region WithFrontChannel Tests

    [Fact]
    public void WithFrontChannel_Always_MarksTheEndpointAsFrontChannel()
    {
        var endpoint = BuildFrontChannelEndpoint(CreateEndpointBuilder(_ => Task.CompletedTask));

        Assert.True(endpoint.IsFrontChannel);
    }

    [Fact]
    public void WithFrontChannel_Always_AcceptsCorsPreflightRequests()
    {
        var endpoint = BuildFrontChannelEndpoint(CreateEndpointBuilder(_ => Task.CompletedTask));

        var httpMethodMetadata = endpoint.Metadata.GetRequiredMetadata<IHttpMethodMetadata>();
        Assert.True(httpMethodMetadata.AcceptCorsPreflight);
        Assert.Equal([HttpMethods.Get, HttpMethods.Post], httpMethodMetadata.HttpMethods);
    }

    [Fact]
    public async Task WithFrontChannel_WhenTheRequestIsACorsPreflight_RefusesItWithMethodNotAllowed()
    {
        var called = false;
        var endpoint = BuildFrontChannelEndpoint(
            CreateEndpointBuilder(_ =>
            {
                called = true;
                return Task.CompletedTask;
            })
        );
        var httpContext = CreatePreflightContext();

        var requestDelegate = endpoint.RequestDelegate;
        Assert.NotNull(requestDelegate);
        await requestDelegate(httpContext);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, httpContext.Response.StatusCode);
        Assert.Equal("GET, POST", httpContext.Response.Headers.Allow.ToString());
    }

    [Fact]
    public async Task WithFrontChannel_WhenTheRequestIsNotACorsPreflight_CallsTheEndpoint()
    {
        var called = false;
        var endpoint = BuildFrontChannelEndpoint(
            CreateEndpointBuilder(_ =>
            {
                called = true;
                return Task.CompletedTask;
            })
        );
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Options;

        var requestDelegate = endpoint.RequestDelegate;
        Assert.NotNull(requestDelegate);
        await requestDelegate(httpContext);

        Assert.True(called);
    }

    [Fact]
    public void WithFrontChannel_WhenTheBuilderIsNull_ThrowsArgumentNullException()
    {
        IEndpointConventionBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.WithFrontChannel());

        Assert.Equal("builder", exception.ParamName);
    }

    #endregion

    #region IsFrontChannel Tests

    [Fact]
    public void IsFrontChannel_WhenNoEndpointIsSelected_ReturnsFalse()
    {
        Endpoint? endpoint = null;

        Assert.False(endpoint.IsFrontChannel);
    }

    [Fact]
    public void IsFrontChannel_WhenTheEndpointHasNoMarker_ReturnsFalse()
    {
        var endpoint = new Endpoint(null, EndpointMetadataCollection.Empty, "test");

        Assert.False(endpoint.IsFrontChannel);
    }

    [Fact]
    public void IsFrontChannel_WhenTheLastMarkerIsNotFrontChannel_ReturnsFalse()
    {
        var endpoint = new Endpoint(
            null,
            new EndpointMetadataCollection(
                new FrontChannelEndpointMetadata(),
                new FrontChannelEndpointMetadata { IsFrontChannel = false }
            ),
            "test"
        );

        Assert.False(endpoint.IsFrontChannel);
    }

    #endregion

    private sealed class RecordingConventionBuilder : IEndpointConventionBuilder
    {
        private List<Action<EndpointBuilder>> Conventions { get; } = [];

        private List<Action<EndpointBuilder>> FinallyConventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);

        public void Finally(Action<EndpointBuilder> finallyConvention) =>
            FinallyConventions.Add(finallyConvention);

        public void Apply(EndpointBuilder endpointBuilder)
        {
            foreach (var convention in Conventions)
            {
                convention(endpointBuilder);
            }

            foreach (var finallyConvention in FinallyConventions)
            {
                finallyConvention(endpointBuilder);
            }
        }
    }
}
