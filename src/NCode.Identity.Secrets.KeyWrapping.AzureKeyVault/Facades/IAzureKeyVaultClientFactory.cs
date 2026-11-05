// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Security.KeyVault.Keys.Cryptography;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;

/// <summary>
/// Provides the Azure Key Vault cryptography client for a key, so the code that wraps and unwraps keys can be tested
/// without calling Azure.
/// </summary>
// See ADR-1260.
internal interface IAzureKeyVaultClientFactory
{
    /// <summary>
    /// Gets the client for the specified key, reaching the key through the Microsoft Entra directory that the access
    /// names, if any. The same client is returned for every call with the same directory and key version identifier,
    /// and a new client for each call with a versionless identifier, so a wrap always uses the key's current version.
    /// </summary>
    /// <param name="keyId">The identifier of a Key Vault or Managed HSM key, with or without a version, such as
    /// <c>https://contoso.vault.azure.net/keys/ncode-identity-root</c>.</param>
    /// <param name="access">The Microsoft Entra directory, as the identity of the access, that tokens for the key are
    /// requested from; or <see langword="null"/> for the directory of the deployment's own identity.</param>
    /// <returns>The <see cref="CryptographyClient"/> that performs cryptographic operations with the key.</returns>
    CryptographyClient GetClient(string keyId, KeyAccess? access);

    /// <summary>
    /// Removes the credential of the directory that the access names, and every client created with it, so that an
    /// access whose calls fail leaves nothing cached. Does nothing when no such credential exists.
    /// </summary>
    /// <param name="access">The Microsoft Entra directory, as the identity of the access, to release.</param>
    void Release(KeyAccess access);
}
