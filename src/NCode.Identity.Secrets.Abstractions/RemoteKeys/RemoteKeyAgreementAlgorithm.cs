// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one row of the mapping from a JOSE key management algorithm to the way a key management service creates a
/// key and agrees a shared secret with it.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed record RemoteKeyAgreementAlgorithm : RemoteKeyAlgorithm
{
    /// <summary>
    /// Gets the key management service's name for the key agreement algorithm, used when the service agrees a shared
    /// secret.
    /// </summary>
    public required string KeyAgreementAlgorithm { get; init; }

    /// <inheritdoc />
    public override string Use => SecretKeyUses.Encryption;
}
