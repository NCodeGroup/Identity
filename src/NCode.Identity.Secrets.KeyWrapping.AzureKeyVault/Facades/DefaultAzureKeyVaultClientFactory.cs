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
/// new <see cref="CryptographyClient"/> for each call with a versionless key identifier, and one
/// <see cref="DefaultAzureCredential"/> shared by every client.
/// </summary>
/// <remarks>
/// A client downloads its key on first use and then wraps locally with that key version for its whole lifetime, so a
/// versionless identifier gets a new client for each call, and a wrap therefore always uses the key's current version.
/// <see cref="DefaultAzureCredential"/> honors the <c>AZURE_TOKEN_CREDENTIALS</c> environment variable (the chart sets
/// it to <c>WorkloadIdentityCredential</c>, so exactly one credential is tried). A client first requests a token on its
/// first call, so creating a client never fails for lack of credentials; the first call that uses one does.
/// </remarks>
// See ADR-1260.
internal sealed class DefaultAzureKeyVaultClientFactory : IAzureKeyVaultClientFactory
{
    private Lazy<TokenCredential> Credential { get; } = new(() => new DefaultAzureCredential());

    private ConcurrentDictionary<string, CryptographyClient> Clients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public CryptographyClient GetClient(string keyId)
    {
        if (AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            return Clients.GetOrAdd(
                keyId,
                static (id, credential) => new CryptographyClient(new Uri(id), credential.Value),
                Credential
            );
        }

        // A client downloads its key on first use and then wraps locally with that key version for its whole lifetime,
        // so a cached client of a versionless identifier would keep wrapping with the version that was current when it
        // was created, even after the key is rotated and that version disabled. Wraps are rare, so downloading the key
        // again for each one is cheap.
        return new CryptographyClient(new Uri(keyId), Credential.Value);
    }
}
