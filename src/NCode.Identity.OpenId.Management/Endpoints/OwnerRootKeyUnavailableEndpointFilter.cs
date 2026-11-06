// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. A request that needs the encryption keys
/// of a tenant whose root key is unavailable is answered <c>503 Service Unavailable</c> with a <c>Retry-After</c> of the
/// retry interval and the same wait in the problem's <c>retryAfterSeconds</c>, because the tenant's data cannot be
/// read until the key is restored and validated again.
/// </summary>
// See ADR-1414.
internal sealed class OwnerRootKeyUnavailableEndpointFilter(
    IOptions<SecretProtectionOptions> options,
    ILogger<OwnerRootKeyUnavailableEndpointFilter> logger
) : IEndpointFilter
{
    private IOptions<SecretProtectionOptions> Options { get; } = options;

    private ILogger<OwnerRootKeyUnavailableEndpointFilter> Logger { get; } = logger;

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
        catch (OwnerRootKeyUnavailableException exception)
        {
            Logger.OwnerRootKeyUnavailable(exception.Owner);

            return RetryAfterProblem.Create(
                context.HttpContext,
                Options.Value.OwnerRootKeys.RetryInterval,
                new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail =
                        "The tenant's root key is unavailable, so its encryption keys cannot be used until it is restored.",
                    Extensions =
                    {
                        [ManagementProblemMembers.OwnerType] = exception.Owner.OwnerType,
                        [ManagementProblemMembers.OwnerId] = exception.Owner.OwnerId,
                    },
                }
            );
        }
    }
}
