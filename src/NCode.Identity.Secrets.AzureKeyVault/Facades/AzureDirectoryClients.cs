// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace NCode.Identity.Secrets.AzureKeyVault.Facades;

/// <summary>
/// Owns the <see cref="DefaultAzureCredential"/> that requests tokens for a Microsoft Entra directory, together with
/// the <see cref="CryptographyClient"/> and <see cref="KeyClient"/> instances that use it, each configured by
/// <see cref="AzureKeyVaultClientOptions"/>.
/// </summary>
/// <remarks>
/// A client downloads its key on first use and then wraps locally with that key version for its whole lifetime, so a
/// versionless identifier gets a new client for each call, and a wrap therefore always uses the key's current version.
/// A client first requests a token on its first call, so creating a client never fails for lack of credentials; the
/// first call that uses one does.
/// </remarks>
// See ADR-1260 and ADR-1429.
internal sealed class AzureDirectoryClients
{
    private DefaultAzureCredential Credential { get; }

    private ConcurrentDictionary<string, CryptographyClient> Clients { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<Uri, KeyClient> KeyClients { get; } = new();

    private AzureDirectoryClients(DefaultAzureCredential credential)
    {
        Credential = credential;
    }

    /// <summary>
    /// Creates the credential and clients of a Microsoft Entra directory.
    /// </summary>
    /// <param name="directory">The identifier of the directory, a GUID, that tokens are requested from; or
    /// <see langword="null"/> for the directory of the deployment's own identity.</param>
    /// <returns>The credential and clients for the directory.</returns>
    public static AzureDirectoryClients Create(string? directory) =>
        new(
            directory is null
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions { TenantId = directory }
                )
        );

    /// <summary>
    /// Gets the client for a key. The same client is returned for every call with the same key version identifier,
    /// and a new client for each call with a versionless identifier.
    /// </summary>
    /// <param name="keyId">The identifier of a Key Vault or Managed HSM key, with or without a version.</param>
    /// <returns>The <see cref="CryptographyClient"/> that performs cryptographic operations with the key.</returns>
    public CryptographyClient GetClient(string keyId)
    {
        if (AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            return Clients.GetOrAdd(
                keyId,
                static (_, state) => CreateClient(state.keyId, state.Credential),
                (keyId, Credential)
            );
        }

        // A cached client of a versionless identifier would keep wrapping with the version that was current when it was
        // created, even after the key is rotated and that version disabled. Wraps are rare, so downloading the key
        // again for each one is cheap.
        return CreateClient(keyId, Credential);
    }

    /// <summary>
    /// Gets the client for the keys of a vault. The same client is returned for every call with the same vault.
    /// </summary>
    /// <param name="vaultUri">The URI of a Key Vault or Managed HSM, such as <c>https://contoso.vault.azure.net</c>.</param>
    /// <returns>The <see cref="KeyClient"/> that creates, reads and disables the keys of the vault.</returns>
    public KeyClient GetKeyClient(Uri vaultUri) =>
        KeyClients.GetOrAdd(
            vaultUri,
            static (uri, credential) =>
                new KeyClient(
                    uri,
                    credential,
                    AzureKeyVaultClientOptions.Configure(new KeyClientOptions())
                ),
            Credential
        );

    private static CryptographyClient CreateClient(
        string keyId,
        DefaultAzureCredential credential
    ) =>
        new(
            new Uri(keyId),
            credential,
            AzureKeyVaultClientOptions.Configure(new CryptographyClientOptions())
        );
}
