// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the master keys of an owner.
/// </summary>
/// <param name="Owner">The owner whose stored secrets the master keys protect.</param>
/// <param name="Keys">Every master key of the owner, newest version first.</param>
/// <param name="RecoveryConfigured">Whether new master keys are also wrapped under a recovery key.</param>
// See ADR-1407 and ADR-1411.
[PublicAPI]
public sealed record MasterKeyStatus(
    SecretOwner Owner,
    ImmutableArray<MasterKeyInfo> Keys,
    bool RecoveryConfigured
)
{
    /// <summary>
    /// Gets the root key the owner brings, or <see langword="null"/> when the deployment's root key wraps the owner's
    /// master keys.
    /// </summary>
    public OwnerRootKeyInfo? RootKey { get; init; }
}
