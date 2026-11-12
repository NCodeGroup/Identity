// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Provides a default implementation of the <see cref="IMaterializedPlatformRegistry"/> abstraction. The first
/// registration wins and completes every wait for the platform.
/// </summary>
// See ADR-1412.
internal sealed class DefaultMaterializedPlatformRegistry : IMaterializedPlatformRegistry
{
    private TaskCompletionSource<OpenIdPlatform> Registered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public void Register(OpenIdPlatform platform)
    {
        // A replica materializes its platform once, so a later registration is ignored.
        ArgumentNullException.ThrowIfNull(platform);
        Registered.TrySetResult(platform);
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdPlatform> GetPlatformAsync(CancellationToken cancellationToken) =>
        await Registered.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask RefreshSettingsAsync(CancellationToken cancellationToken)
    {
        if (!Registered.Task.IsCompletedSuccessfully)
            return;

        var platform = await Registered.Task;
        await platform.RefreshSettingsAsync(cancellationToken);
    }
}
