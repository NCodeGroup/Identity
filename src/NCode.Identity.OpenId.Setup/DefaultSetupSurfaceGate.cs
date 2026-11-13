// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of the <see cref="ISetupSurfaceGate"/> abstraction. The surface is served while
/// the instance is not initialized, and in Production only over HTTPS. A Production request that is not HTTPS is
/// refused without reading the instance.
/// </summary>
internal sealed class DefaultSetupSurfaceGate(
    IInstanceSetupService instanceSetupService,
    IHostEnvironment hostEnvironment
) : ISetupSurfaceGate
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

    /// <inheritdoc />
    public async ValueTask<bool> IsServedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (HostEnvironment.IsProduction() && !httpContext.Request.IsHttps)
        {
            return false;
        }

        return !await InstanceSetupService.IsInitializedAsync(cancellationToken);
    }
}
