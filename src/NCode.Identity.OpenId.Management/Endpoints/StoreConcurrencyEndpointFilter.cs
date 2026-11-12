// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. It answers a write that lost a concurrency
/// race with <c>412 Precondition Failed</c>, the same answer as a stale <c>If-Match</c>, so that the caller reads the
/// resource again and retries.
/// </summary>
// See ADR-1012 and ADR-1483.
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
