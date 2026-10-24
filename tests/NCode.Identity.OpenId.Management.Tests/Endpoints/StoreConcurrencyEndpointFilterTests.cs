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
