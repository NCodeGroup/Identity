// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class StoreConcurrencyEndpointFilterTests
{
    private static StoreConcurrencyEndpointFilter CreateFilter() =>
        new(NullLogger<StoreConcurrencyEndpointFilter>.Instance);

    private static EndpointFilterInvocationContext CreateContext() =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext());

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointLosesAConcurrencyRace_ReturnsPreconditionFailed()
    {
        var filter = CreateFilter();
        var context = CreateContext();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(new StoreConcurrencyException("conflict"));

        var result = await filter.InvokeAsync(context, next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, problem.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointSucceeds_ReturnsItsResult()
    {
        var filter = CreateFilter();
        var context = CreateContext();
        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);

        var result = await filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointFailsOtherwise_Propagates()
    {
        var filter = CreateFilter();
        var context = CreateContext();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(new InvalidOperationException("failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.InvokeAsync(context, next).AsTask()
        );
    }

    #endregion
}
