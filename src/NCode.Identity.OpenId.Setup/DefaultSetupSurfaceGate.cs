// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of the <see cref="ISetupSurfaceGate"/> abstraction. The surface is served while
/// the instance is not initialized, and only over HTTPS. When <see cref="LocalDevelopmentMode.Enabled"/> is set, a
/// plain HTTP request is also served if it comes from a loopback remote address; a request without a remote address is
/// not loopback. A request that is neither is refused without reading the instance.
/// </summary>
internal sealed class DefaultSetupSurfaceGate(
    IInstanceSetupService instanceSetupService,
    LocalDevelopmentMode localDevelopment
) : ISetupSurfaceGate
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private LocalDevelopmentMode LocalDevelopment { get; } = localDevelopment;

    /// <inheritdoc />
    public async ValueTask<bool> IsServedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!IsTransportAdmitted(httpContext))
        {
            return false;
        }

        return !await InstanceSetupService.IsInitializedAsync(cancellationToken);
    }

    private bool IsTransportAdmitted(HttpContext httpContext)
    {
        if (httpContext.Request.IsHttps)
        {
            return true;
        }

        return LocalDevelopment.Enabled
            && httpContext.Connection.RemoteIpAddress is { } address
            && IPAddress.IsLoopback(address);
    }
}
