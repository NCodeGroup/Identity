// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;

/// <summary>
/// Runs calls with the Azure Key Vault cryptography client for a key and the key client for a vault, so the code that
/// wraps and unwraps keys and the code that creates keys can be tested without calling Azure.
/// </summary>
// See ADR-1260.
internal interface IAzureKeyVaultClientFactory
{
    /// <summary>
    /// Runs an operation with the client for the specified key, reaching the key through the Microsoft Entra directory
    /// that the access names, if any. The same client serves every call with the same directory and key version
    /// identifier, and a new client serves each call with a versionless identifier, so a wrap always uses the key's
    /// current version.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
    /// <param name="keyId">The identifier of a Key Vault or Managed HSM key, with or without a version, such as
    /// <c>https://contoso.vault.azure.net/keys/ncode-identity-root</c>.</param>
    /// <param name="access">The Microsoft Entra directory, as the identity of the access, that tokens for the key are
    /// requested from; or <see langword="null"/> for the directory of the deployment's own identity.</param>
    /// <param name="operation">The operation to run with the <see cref="CryptographyClient"/> that performs
    /// cryptographic operations with the key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteAsync<TResult>(
        string keyId,
        KeyAccess? access,
        Func<CryptographyClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Runs an operation with the client for the keys of a vault, with the deployment's own identity. The same client
    /// serves every call with the same vault.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
    /// <param name="vaultUri">The URI of a Key Vault or Managed HSM, such as <c>https://contoso.vault.azure.net</c>.</param>
    /// <param name="operation">The operation to run with the <see cref="KeyClient"/> that creates, reads and disables
    /// the keys of the vault.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteKeyClientAsync<TResult>(
        Uri vaultUri,
        Func<KeyClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    );
}
