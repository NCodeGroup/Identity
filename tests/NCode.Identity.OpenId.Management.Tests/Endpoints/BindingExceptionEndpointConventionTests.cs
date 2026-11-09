// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class BindingExceptionEndpointConventionTests
{
    private static DefaultHttpContext CreateHttpContext() =>
        new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };

    #region Apply Tests

    [Fact]
    public async Task Apply_WhenTheEndpointThrowsABindingFailure_AnswersItsStatusWithNoBody()
    {
        var httpContext = CreateHttpContext();
        var builder = new RouteEndpointBuilder(
            _ =>
                throw new BadHttpRequestException(
                    "Unsupported media type.",
                    StatusCodes.Status415UnsupportedMediaType
                ),
            RoutePatternFactory.Parse("/"),
            0
        );

        BindingExceptionEndpointConvention.Apply(builder);
        await builder.RequestDelegate!(httpContext);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, httpContext.Response.StatusCode);
        Assert.Null(httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Fact]
    public async Task Apply_WhenTheEndpointSetsAnErrorStatus_LeavesTheResponseUntouched()
    {
        var httpContext = CreateHttpContext();
        var builder = new RouteEndpointBuilder(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            },
            RoutePatternFactory.Parse("/"),
            0
        );

        BindingExceptionEndpointConvention.Apply(builder);
        await builder.RequestDelegate!(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Null(httpContext.Response.ContentType);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Fact]
    public async Task Apply_WhenTheResponseHasStartedAndBindingFails_RethrowsTheFailure()
    {
        var httpContext = CreateHttpContext();
        httpContext.Features.Set<IHttpResponseFeature>(
            new StartedResponseFeature { Body = httpContext.Response.Body }
        );
        var builder = new RouteEndpointBuilder(
            _ =>
                throw new BadHttpRequestException(
                    "Unsupported media type.",
                    StatusCodes.Status415UnsupportedMediaType
                ),
            RoutePatternFactory.Parse("/"),
            0
        );

        BindingExceptionEndpointConvention.Apply(builder);

        await Assert.ThrowsAsync<BadHttpRequestException>(() =>
            builder.RequestDelegate!(httpContext)
        );
    }

    [Fact]
    public void Apply_WhenTheEndpointHasNoRequestDelegate_Throws()
    {
        var builder = new RouteEndpointBuilder(null, RoutePatternFactory.Parse("/"), 0);

        Assert.Throws<InvalidOperationException>(() =>
            BindingExceptionEndpointConvention.Apply(builder)
        );
    }

    #endregion

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
