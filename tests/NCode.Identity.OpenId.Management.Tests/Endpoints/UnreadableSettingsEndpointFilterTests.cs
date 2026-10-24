#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
