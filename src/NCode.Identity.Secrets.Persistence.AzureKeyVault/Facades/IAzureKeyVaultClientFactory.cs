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

using Azure.Security.KeyVault.Keys.Cryptography;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault.Facades;

/// <summary>
/// Provides the Azure Key Vault cryptography client for a key, so the code that wraps and unwraps keys can be tested
/// without calling Azure.
/// </summary>
// See ADR-0238.
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
