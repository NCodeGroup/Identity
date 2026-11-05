// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. A request that needs the encryption keys
/// of a tenant whose root key is unavailable is answered <c>503 Service Unavailable</c> with a <c>Retry-After</c> of the
/// retry interval, because the tenant's data cannot be read until the key is restored and validated again.
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

            var retryAfter = Math.Max(
                1,
                (long)Math.Ceiling(Options.Value.OwnerRootKeys.RetryInterval.TotalSeconds)
            );
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(
                CultureInfo.InvariantCulture
            );

            return TypedResults.Problem(
                detail: "The tenant's root key is unavailable, so its encryption keys cannot be used until it is restored.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?>
                {
                    ["ownerType"] = exception.Owner.OwnerType,
                    ["ownerId"] = exception.Owner.OwnerId,
                }
            );
        }
    }
}
