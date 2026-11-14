// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of the <see cref="ISetupSurfaceGate"/> abstraction. The surface is served while
/// the instance is not initialized, and only over HTTPS. When <see cref="LocalDevelopmentOptions.Enabled"/> is set, a
/// plain HTTP request is also served if it comes from a loopback remote address; a request without a remote address is
/// not loopback. A request that is neither is refused without reading the instance.
/// </summary>
internal sealed class DefaultSetupSurfaceGate(
    IInstanceSetupService instanceSetupService,
    IOptions<LocalDevelopmentOptions> localDevelopmentOptions
) : ISetupSurfaceGate
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private IOptions<LocalDevelopmentOptions> LocalDevelopmentOptionsAccessor { get; } =
        localDevelopmentOptions;

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

        return LocalDevelopmentOptionsAccessor.Value.Enabled
            && httpContext.Connection.RemoteIpAddress is { } address
            && IPAddress.IsLoopback(address);
    }
}
