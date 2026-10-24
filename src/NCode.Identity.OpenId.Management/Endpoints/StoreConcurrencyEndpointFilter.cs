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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. It answers a write that lost a concurrency
/// race with <c>412 Precondition Failed</c>, the same answer as a stale <c>If-Match</c>, so that the caller reads the
/// resource again and retries.
/// </summary>
// See ADR-0208 and ADR-0214.
internal sealed class StoreConcurrencyEndpointFilter(ILogger<StoreConcurrencyEndpointFilter> logger)
    : IEndpointFilter
{
    private ILogger<StoreConcurrencyEndpointFilter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        try
        {
            return await next(context);
        }
        catch (StoreConcurrencyException exception)
        {
            Logger.LostConcurrencyRace(exception);
            return TypedResults.Problem(
                detail: "The resource was changed by another request after this request read it. Read it again and retry.",
                statusCode: StatusCodes.Status412PreconditionFailed
            );
        }
    }
}
