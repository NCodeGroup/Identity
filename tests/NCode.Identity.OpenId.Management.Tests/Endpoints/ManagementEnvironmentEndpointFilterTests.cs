// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Results;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ManagementEnvironmentEndpointFilterTests
{
    private static (
        ManagementEnvironmentEndpointFilter Filter,
        EndpointFilterInvocationContext Context,
        Mock<IOpenIdContextFactory> ContextFactory
    ) Create(MockRepository mocks)
    {
        var contextFactory = mocks.Create<IOpenIdContextFactory>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(Mock.Of<IMediator>())
                .BuildServiceProvider(),
        };

        return (
            new ManagementEnvironmentEndpointFilter(contextFactory.Object),
            EndpointFilterInvocationContext.Create(httpContext),
            contextFactory
        );
    }

    private static void SetupMaterialization(Mock<IOpenIdContextFactory> contextFactory) =>
        contextFactory
            .Setup(x =>
                x.CreateForControlPlaneAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Mock.Of<OpenIdContext>())
            .Verifiable();

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheRouteThrowsAnHttpResult_ReturnsItsResult()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var (filter, context, contextFactory) = Create(mocks);
        SetupMaterialization(contextFactory);
        var refusal = TypedResults.NotFound();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(refusal.AsException("x"));

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(refusal, result);
        mocks.Verify();
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestBindingFailed_ReturnsEmptyWithoutMaterializingTheContext()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var (filter, context, _) = Create(mocks);
        context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        EndpointFilterDelegate next = _ =>
            throw new InvalidOperationException("The route must not run.");

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(TypedResults.Empty, result);
        mocks.Verify();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheContextCannotBeMaterialized_ReturnsItsResultWithoutCallingTheRoute()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var (filter, context, contextFactory) = Create(mocks);
        var refusal = TypedResults.NotFound();
        contextFactory
            .Setup(x =>
                x.CreateForControlPlaneAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(refusal.AsException("x"))
            .Verifiable();
        EndpointFilterDelegate next = _ =>
            throw new InvalidOperationException("The route must not run.");

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(refusal, result);
        mocks.Verify();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRouteSucceeds_ReturnsItsResult()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var (filter, context, contextFactory) = Create(mocks);
        SetupMaterialization(contextFactory);
        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
        mocks.Verify();
    }

    #endregion
}
