// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Binds a key that a key management service already holds to a tenant. It reads the key's public half from the
/// service and stores it as a <c>remote</c> secret, then proves the binding by the algorithm's kind: a signing key
/// signs 32 random bytes that the stored public half verifies, and an encryption key decrypts 32 random bytes the
/// server encrypts to the stored public half as a compact JWE (<c>A256GCM</c>), so a key that does not hold the
/// private half of the public half the service returned is never stored.
/// </summary>
// See ADR-1475.
// See ADR-1476.
// See ADR-1477.
internal interface IRemoteSecretImporter
{
    /// <summary>
    /// Binds a key that a key management service holds as a <c>remote</c> secret that references it: reads the key's
    /// public half from the service and checks it against the request's remote signing algorithm
    /// (<see cref="ISecretGenerator.BindRemoteSecretAsync"/>). The secret is <c>supplied</c>, so the server never
    /// destroys the key.
    /// </summary>
    /// <param name="request">The key's reference, type, size, curve, algorithm and lifecycle.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The bound secret; a <c>400</c> when the provider refuses the key or its public half is not a key of the
    /// request's type, size and curve; or a <c>502</c> when the key management service fails, refuses the deployment's
    /// identity, or cannot be reached.
    /// </returns>
    ValueTask<RemoteSecretImportResult> ImportAsync(
        BindRemoteSecretRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Proves that the key a <c>remote</c> secret references holds the private half of the secret's public key, by the
    /// secret's algorithm: a signing key signs a random challenge through the referenced key and the stored public
    /// half verifies the signature; an encryption key decrypts a random challenge that the server encrypts to the
    /// stored public half as a compact JWE (<c>A256GCM</c>).
    /// </summary>
    /// <param name="secret">The <c>remote</c> secret to prove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <c>null</c> when the key holds the private half of the public key; a <c>400</c> when the secret cannot be read
    /// as a remote key, its algorithm is not a registered signature or key-management algorithm, or the signature does
    /// not verify or the decryption does not return the challenge; or a <c>502</c> when the key management service
    /// fails or refuses the request.
    /// </returns>
    ValueTask<ManagementError?> VerifyBindingAsync(
        PersistedSecret secret,
        CancellationToken cancellationToken
    );
}
