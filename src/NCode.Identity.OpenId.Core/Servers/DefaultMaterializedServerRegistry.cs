#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IMaterializedServerRegistry"/> abstraction. The first
/// registration wins and completes every wait for the server.
/// </summary>
// See ADR-0219.
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
