// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides a default implementation of the <see cref="IRemoteSecretDestroyer"/> abstraction that destroys the key of
/// each removed remote secret that the server generated, through the <see cref="IRemoteKeyProvider"/> that the
/// secret's reference names. Each destruction runs on a bounded token of its own through
/// <see cref="PostCommitWork"/>, and a failure is logged with the secret identifier and the provider name only.
/// </summary>
// See ADR-1468 and ADR-1471.
internal class DefaultRemoteSecretDestroyer(
    IRemoteKeyProviderCatalog remoteKeyProviderCatalog,
    TimeProvider timeProvider,
    ILogger<DefaultRemoteSecretDestroyer> logger
) : IRemoteSecretDestroyer
{
    private IRemoteKeyProviderCatalog RemoteKeyProviderCatalog { get; } = remoteKeyProviderCatalog;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultRemoteSecretDestroyer> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask DestroyAsync(IReadOnlyCollection<PersistedSecret> removedSecrets)
    {
        foreach (var secret in removedSecrets)
        {
            if (
                !SecretEncodings.IsRemote(secret.SecretEncoding)
                || !string.Equals(secret.Origin, SecretOrigins.Generated, StringComparison.Ordinal)
            )
            {
                continue;
            }

            await DestroyOneAsync(secret);
        }
    }

    /// <inheritdoc />
    // See ADR-1478.
    public async ValueTask<bool> DestroyKeyAsync(RemoteKeyReference reference)
    {
        if (!RemoteKeyProviderCatalog.TryGetProvider(reference.Provider, out var provider))
        {
            Logger.RemoteKeyOrphanDestroySkipped(reference.Provider, reference.KeyId);
            return false;
        }

        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static (state, cancellationToken) =>
                    state.Provider.DestroyKeyAsync(state.KeyId, cancellationToken),
                (Provider: provider, reference.KeyId)
            );
            return true;
        }
        catch (Exception exception)
        {
            Logger.RemoteKeyOrphanDestroyFailed(exception, reference.Provider, reference.KeyId);
            return false;
        }
    }

    // Destroys the key of one secret and swallows every failure, since the removal is committed.
    private async ValueTask DestroyOneAsync(PersistedSecret secret)
    {
        if (!RemoteKeyReference.TryParse(secret.EncodedValue, out var reference))
        {
            Logger.RemoteKeyDestroySkipped(secret.SecretId, string.Empty);
            return;
        }

        if (!RemoteKeyProviderCatalog.TryGetProvider(reference.Provider, out var provider))
        {
            Logger.RemoteKeyDestroySkipped(secret.SecretId, reference.Provider);
            return;
        }

        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static (state, cancellationToken) =>
                    state.Provider.DestroyKeyAsync(state.KeyId, cancellationToken),
                (Provider: provider, reference.KeyId)
            );
        }
        catch (Exception exception)
        {
            Logger.RemoteKeyDestroyFailed(exception, secret.SecretId, reference.Provider);
        }
    }
}
