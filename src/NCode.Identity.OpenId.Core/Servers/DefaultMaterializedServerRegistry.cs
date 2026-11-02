// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IMaterializedServerRegistry"/> abstraction. The first
/// registration wins and completes every wait for the server.
/// </summary>
// See ADR-1219.
internal sealed class DefaultMaterializedServerRegistry : IMaterializedServerRegistry
{
    private TaskCompletionSource<OpenIdServer> Registered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc />
    public void Register(OpenIdServer server)
    {
        // A replica materializes its server once, so a later registration is ignored.
        ArgumentNullException.ThrowIfNull(server);
        Registered.TrySetResult(server);
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdServer> GetServerAsync(CancellationToken cancellationToken) =>
        await Registered.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask RefreshSettingsAsync(
        string serverId,
        CancellationToken cancellationToken
    )
    {
        if (!Registered.Task.IsCompletedSuccessfully)
            return;

        var server = await Registered.Task;
        if (!string.Equals(server.ServerId, serverId, StringComparison.Ordinal))
            return;

        await server.RefreshSettingsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RefreshSettingsAsync(CancellationToken cancellationToken)
    {
        if (!Registered.Task.IsCompletedSuccessfully)
            return;

        var server = await Registered.Task;
        await server.RefreshSettingsAsync(cancellationToken);
    }
}
