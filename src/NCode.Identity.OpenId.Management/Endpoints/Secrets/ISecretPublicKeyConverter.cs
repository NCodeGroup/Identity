// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Converts a persisted secret into its public key material (JWK and PEM) for the public-key read endpoints,
/// returning <c>null</c> for a symmetric secret that has no public key to expose.
/// </summary>
internal interface ISecretPublicKeyConverter
{
    /// <summary>
    /// Converts the specified persisted secret into its <see cref="PublicKeyResource"/> from its public key material
    /// alone, without unprotecting any private material, or returns <c>null</c> when the secret has no public key
    /// material: a symmetric secret, a secret whose key type exports no public key, or a private key stored without
    /// its public key.
    /// </summary>
    /// <param name="secret">The persisted secret to convert.</param>
    /// <returns>The <see cref="PublicKeyResource"/>, or <c>null</c> when the secret has no public key material.</returns>
    // See ADR-1268.
    PublicKeyResource? Convert(PersistedSecret secret);
}
