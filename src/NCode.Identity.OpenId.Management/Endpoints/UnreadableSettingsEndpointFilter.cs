// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// An endpoint filter that is installed once on the management route group. A request that needs a stored settings
/// document the registered settings cannot honor is answered <c>409 Conflict</c> with a problem that names the
/// document, so that the caller can replace it with a settings write.
/// </summary>
// See ADR-1245.
internal sealed class UnreadableSettingsEndpointFilter(
    ILogger<UnreadableSettingsEndpointFilter> logger
) : IEndpointFilter
{
    private ILogger<UnreadableSettingsEndpointFilter> Logger { get; } = logger;

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
        catch (UnreadableSettingsException exception)
        {
            Logger.StoredSettingsUnreadable(
                exception.ScopeName,
                exception.OwnerId,
                exception.DocumentError
            );

            var detail = $"{exception.Message} Replace the document with a settings write.";
            var extensions = new Dictionary<string, object?>
            {
                [ManagementProblemMembers.SettingsScope] = exception.ScopeName,
                [ManagementProblemMembers.OwnerId] = exception.OwnerId,
                [ManagementProblemMembers.DocumentError] = exception.DocumentError,
            };

            return TypedResults.Problem(
                detail: detail,
                statusCode: StatusCodes.Status409Conflict,
                extensions: extensions
            );
        }
    }
}
