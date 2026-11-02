// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class UnreadableSettingsEndpointFilterTests
{
    private static UnreadableSettingsEndpointFilter CreateFilter() =>
        new(NullLogger<UnreadableSettingsEndpointFilter>.Instance);

    private static EndpointFilterInvocationContext CreateContext() =>
        EndpointFilterInvocationContext.Create(new DefaultHttpContext());

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheEndpointMeetsAnUnreadableDocument_ReturnsConflictNamingIt()
    {
        var filter = CreateFilter();
        var context = CreateContext();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(
                new UnreadableSettingsException("tenant", "tenant-1", new JsonException("boom"))
            );

        var result = await filter.InvokeAsync(context, next);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("tenant", problem.ProblemDetails.Extensions["settingsScope"]);
        Assert.Equal("tenant-1", problem.ProblemDetails.Extensions["ownerId"]);
        Assert.Equal("boom", problem.ProblemDetails.Extensions["documentError"]);
        Assert.Contains("tenant 'tenant-1'", problem.ProblemDetails.Detail);
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
    public async Task InvokeAsync_WhenTheEndpointThrowsAnotherJsonException_Propagates()
    {
        var filter = CreateFilter();
        var context = CreateContext();
        EndpointFilterDelegate next = _ =>
            ValueTask.FromException<object?>(new JsonException("failure"));

        await Assert.ThrowsAsync<JsonException>(() => filter.InvokeAsync(context, next).AsTask());
    }

    #endregion
}
