// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.AzureKeyVault.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAzureKeyVaultClientFactory"/>: one
/// <see cref="CryptographyClient"/> for each key version identifier, created on first use and shared by every caller, a
/// new <see cref="CryptographyClient"/> for each call with a versionless key identifier, one
/// <see cref="DefaultAzureCredential"/> shared by every client of the deployment's own identity, one
/// <see cref="KeyClient"/> for each vault, one more <see cref="KeyClient"/> for each vault that never retries, and one
/// <see cref="AzureDirectoryClients"/> for each Microsoft Entra directory that an access names, held by a
/// <see cref="KeyAccessClientCache{TClient}"/>.
/// </summary>
/// <remarks>
/// A client downloads its key on first use and then wraps locally with that key version for its whole lifetime, so a
/// versionless identifier gets a new client for each call, and a wrap therefore always uses the key's current version.
/// <see cref="DefaultAzureCredential"/> honors the <c>AZURE_TOKEN_CREDENTIALS</c> environment variable (the chart sets
/// it to <c>WorkloadIdentityCredential</c>, so exactly one credential is tried). A directory gets its own credential,
/// which requests tokens for that directory, so the deployment's multi-tenant application reaches a key in a directory
/// that consented to it. The cache keeps the credential and clients of a directory once a call through it succeeds.
/// </remarks>
// See ADR-1260 and ADR-1429.
internal sealed class DefaultAzureKeyVaultClientFactory(TimeProvider timeProvider)
    : IAzureKeyVaultClientFactory,
        IDisposable
{
    private static TimeSpan DirectoryIdleTimeout { get; } = TimeSpan.FromDays(1);

    private Lazy<AzureDirectoryClients> OwnClients { get; } =
        new(() => AzureDirectoryClients.Create(directory: null));

    private KeyAccessClientCache<AzureDirectoryClients> DirectoryClients { get; } =
        new(timeProvider, DirectoryIdleTimeout);

    /// <inheritdoc />
    public ValueTask<TResult> ExecuteAsync<TResult>(
        string keyId,
        KeyAccess? access,
        Func<CryptographyClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (access is null)
        {
            return operation(OwnClients.Value.GetClient(keyId), cancellationToken);
        }

        return DirectoryClients.UseAsync(
            access.Identity,
            _ => ValueTask.FromResult(AzureDirectoryClients.Create(access.Identity)),
            (clients, token) => operation(clients.GetClient(keyId), token),
            cancellationToken
        );
    }

    /// <inheritdoc />
    public ValueTask<TResult> ExecuteKeyClientAsync<TResult>(
        Uri vaultUri,
        Func<KeyClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(vaultUri);
        ArgumentNullException.ThrowIfNull(operation);

        return operation(OwnClients.Value.GetKeyClient(vaultUri), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<TResult> ExecuteKeyClientWithoutRetryAsync<TResult>(
        Uri vaultUri,
        Func<KeyClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(vaultUri);
        ArgumentNullException.ThrowIfNull(operation);

        return operation(OwnClients.Value.GetKeyClientWithoutRetry(vaultUri), cancellationToken);
    }

    /// <summary>
    /// Disposes the cache of the clients of the directories.
    /// </summary>
    public void Dispose() => DirectoryClients.Dispose();
}
