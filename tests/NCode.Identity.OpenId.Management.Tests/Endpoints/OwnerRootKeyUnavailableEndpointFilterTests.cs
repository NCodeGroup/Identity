// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class OwnerRootKeyUnavailableEndpointFilterTests
{
    private static OwnerRootKeyUnavailableEndpointFilter CreateFilter(
        TimeSpan? retryInterval = null
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions
                {
                    OwnerRootKeys = new OwnerRootKeyOptions
                    {
                        RetryInterval = retryInterval ?? TimeSpan.FromMinutes(1),
                    },
                }
            ),
            NullLogger<OwnerRootKeyUnavailableEndpointFilter>.Instance
        );

    private static EndpointFilterInvocationContext CreateContext(HttpContext httpContext) =>
        EndpointFilterInvocationContext.Create(httpContext);

    [Fact]
    public async Task InvokeAsync_WhenTheOwnersRootKeyIsUnavailable_ReturnsServiceUnavailableWithRetryAfter()
    {
        var httpContext = new DefaultHttpContext();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(
                new OwnerRootKeyUnavailableException(SecretOwner.ForTenant("tenant-1"))
            );

        var result = await CreateFilter(TimeSpan.FromSeconds(90.2))
            .InvokeAsync(CreateContext(httpContext), next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("91", httpContext.Response.Headers.RetryAfter.ToString());
        Assert.Equal("tenant", problem.ProblemDetails.Extensions["ownerType"]);
        Assert.Equal("tenant-1", problem.ProblemDetails.Extensions["ownerId"]);
        Assert.Contains("root key is unavailable", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointSucceeds_ReturnsItsResult()
    {
        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);

        var result = await CreateFilter()
            .InvokeAsync(CreateContext(new DefaultHttpContext()), next);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointThrowsAnotherFailure_Propagates()
    {
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateFilter().InvokeAsync(CreateContext(new DefaultHttpContext()), next)
        );
    }
}
