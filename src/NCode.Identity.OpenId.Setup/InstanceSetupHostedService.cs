// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Prepares an uninitialized instance when the host starts: it ensures that a setup token is available through
/// <see cref="ISetupTokenService.EnsureTokenAsync"/>. An initialized instance needs none.
/// </summary>
internal sealed class InstanceSetupHostedService(
    IInstanceSetupService instanceSetupService,
    ISetupTokenService setupTokenService
) : IHostedService
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private ISetupTokenService SetupTokenService { get; } = setupTokenService;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await InstanceSetupService.IsInitializedAsync(cancellationToken))
            return;

        await SetupTokenService.EnsureTokenAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
