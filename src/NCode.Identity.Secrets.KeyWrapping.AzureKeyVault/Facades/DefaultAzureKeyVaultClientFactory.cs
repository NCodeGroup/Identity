// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAzureKeyVaultClientFactory"/>: one
/// <see cref="CryptographyClient"/> for each key version identifier, created on first use and shared by every caller, a
/// new <see cref="CryptographyClient"/> for each call with a versionless key identifier, one
/// <see cref="DefaultAzureCredential"/> shared by every client of the deployment's own identity, and one
/// <see cref="DefaultAzureCredential"/> for each Microsoft Entra directory that an access names.
/// </summary>
/// <remarks>
/// A client downloads its key on first use and then wraps locally with that key version for its whole lifetime, so a
/// versionless identifier gets a new client for each call, and a wrap therefore always uses the key's current version.
/// <see cref="DefaultAzureCredential"/> honors the <c>AZURE_TOKEN_CREDENTIALS</c> environment variable (the chart sets
/// it to <c>WorkloadIdentityCredential</c>, so exactly one credential is tried). A directory gets its own credential,
/// which requests tokens for that directory, so the deployment's multi-tenant application reaches a key in a directory
/// that consented to it. A client first requests a token on its first call, so creating a client never fails for lack
/// of credentials; the first call that uses one does. A directory is confirmed after its first successful call and its
/// credential and clients are then kept for the lifetime of the factory; a directory whose calls have not succeeded
/// is removed when the wrapper releases it after a failed call, so the next call creates it again, while a failure of
/// a confirmed directory (a throttle, for example) leaves its credential in use.
/// </remarks>
// See ADR-1260 and ADR-1429.
internal sealed class DefaultAzureKeyVaultClientFactory : IAzureKeyVaultClientFactory
{
    private Lazy<TokenCredential> Credential { get; } = new(() => new DefaultAzureCredential());

    private ConcurrentDictionary<string, DirectoryEntry> DirectoryCredentials { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, CryptographyClient> Clients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public CryptographyClient GetClient(string keyId, KeyAccess? access)
    {
        var credential = GetCredential(access);

        if (AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            return Clients.GetOrAdd(
                $"{access?.Identity}|{keyId}",
                static (_, state) =>
                    new CryptographyClient(new Uri(state.keyId), state.credential.Value),
                (keyId, credential)
            );
        }

        // A client downloads its key on first use and then wraps locally with that key version for its whole lifetime,
        // so a cached client of a versionless identifier would keep wrapping with the version that was current when it
        // was created, even after the key is rotated and that version disabled. Wraps are rare, so downloading the key
        // again for each one is cheap.
        return new CryptographyClient(new Uri(keyId), credential.Value);
    }

    /// <inheritdoc />
    public void Confirm(KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (DirectoryCredentials.TryGetValue(access.Identity, out var entry))
        {
            entry.Confirm();
        }
    }

    /// <inheritdoc />
    public void Release(KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        // The clients of the deployment's own identity have an empty identity in their key, so a release never reaches
        // them.
        if (string.IsNullOrEmpty(access.Identity))
        {
            return;
        }

        if (!DirectoryCredentials.TryGetValue(access.Identity, out var entry) || entry.IsConfirmed)
        {
            return;
        }

        // Removes exactly the entry that was read, so an entry that another caller has since added stays cached.
        DirectoryCredentials.TryRemove(
            new KeyValuePair<string, DirectoryEntry>(access.Identity, entry)
        );

        var prefix = $"{access.Identity}|";
        foreach (var pair in Clients)
        {
            if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                Clients.TryRemove(pair);
            }
        }
    }

    private Lazy<TokenCredential> GetCredential(KeyAccess? access)
    {
        if (access is null)
        {
            return Credential;
        }

        return DirectoryCredentials
            .GetOrAdd(
                access.Identity,
                static directory => new DirectoryEntry(
                    new Lazy<TokenCredential>(() =>
                        new DefaultAzureCredential(
                            new DefaultAzureCredentialOptions { TenantId = directory }
                        )
                    )
                )
            )
            .Credential;
    }

    /// <summary>
    /// The credential for a Microsoft Entra directory and whether a call through it has succeeded.
    /// </summary>
    private sealed class DirectoryEntry(Lazy<TokenCredential> credential)
    {
        // Read and written with Volatile, because the entry is shared by concurrent calls.
        private bool _confirmed;

        public Lazy<TokenCredential> Credential { get; } = credential;

        public bool IsConfirmed => Volatile.Read(ref _confirmed);

        public void Confirm() => Volatile.Write(ref _confirmed, true);
    }
}
