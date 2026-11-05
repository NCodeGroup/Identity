// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Describes a rewrap of the persisted master keys.
/// </summary>
// See ADR-1408 and ADR-1411.
[PublicAPI]
public sealed class MasterKeyRewrapRequest
{
    /// <summary>
    /// Gets a value indicating whether only the recovery copies are replaced, leaving every root wrap as it is. A
    /// recovery-only rewrap needs a configured recovery public key.
    /// </summary>
    public bool RecoveryOnly { get; init; }

    /// <summary>
    /// Gets the owner whose master keys are rewrapped, or <see langword="null"/> for the master keys of every owner.
    /// </summary>
    public SecretOwner? Owner { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rewrap only proves that every master key can be unwrapped and writes
    /// nothing.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether the rewrap leaves a master key that already conforms to the root of its owner
    /// as it is, without unwrapping it. A master key conforms when it is wrapped under the owner's root key, or, for an
    /// owner without one, under the deployment's root key.
    /// </summary>
    public bool NonconformingOnly { get; init; }

    /// <summary>
    /// Gets the PEM text of the recovery private key that unwraps the master keys from their recovery copies, or an
    /// empty value to unwrap each master key with the root key provider it names. The caller owns the buffer and zeroes
    /// it after the rewrap.
    /// </summary>
    public ReadOnlyMemory<char> RecoveryPrivateKeyPem { get; init; }
}
