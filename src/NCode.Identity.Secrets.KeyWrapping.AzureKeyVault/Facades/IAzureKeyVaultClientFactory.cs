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
    /// Gets the client for the specified key. The same client is returned for every call with the same key version
    /// identifier, and a new client for each call with a versionless identifier, so a wrap always uses the key's
    /// current version.
    /// </summary>
    /// <param name="keyId">The identifier of a Key Vault or Managed HSM key, with or without a version, such as
    /// <c>https://contoso.vault.azure.net/keys/ncode-identity-root</c>.</param>
    /// <returns>The <see cref="CryptographyClient"/> that performs cryptographic operations with the key.</returns>
    CryptographyClient GetClient(string keyId);
}
