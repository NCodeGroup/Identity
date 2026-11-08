// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Binds a key that a key management service already holds to a tenant. It imports the key's supplied public half
/// as a <c>remote</c> secret, then proves the binding by signing a random challenge through the referenced key and
/// verifying the signature with the stored public half, so a key that does not sign with the supplied public half is
/// never stored.
/// </summary>
// See ADR-1475.
internal interface IRemoteSecretImporter
{
    /// <summary>
    /// Imports the public half of a key that a key management service holds, under the client-key policy, as a
    /// <c>remote</c> secret that references the key. The secret is <c>supplied</c>, so the server never destroys the
    /// key in the service.
    /// </summary>
    /// <param name="request">The public half of the key, the metadata to import it with, and the key's reference.</param>
    /// <param name="secret">When this method returns <c>true</c>, the <c>remote</c> secret.</param>
    /// <param name="error">When this method returns <c>false</c>, a human-readable reason the key is refused.</param>
    /// <returns><c>true</c> when the key was imported; otherwise <c>false</c>.</returns>
    bool TryImport(
        RemoteSecretImportRequest request,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    );

    /// <summary>
    /// Proves that the key a <c>remote</c> secret references signs with the private half of the secret's public key:
    /// it signs a random challenge through the referenced key, and the signature is verified with the stored public
    /// half.
    /// </summary>
    /// <param name="secret">The <c>remote</c> secret to prove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <c>null</c> when the key signs with the private half of the public key; a <c>400</c> when the secret cannot be
    /// read as a remote key or the signature does not verify; or a <c>502</c> when the key management service fails or
    /// refuses the request.
    /// </returns>
    ValueTask<ManagementError?> VerifyBindingAsync(
        PersistedSecret secret,
        CancellationToken cancellationToken
    );
}
