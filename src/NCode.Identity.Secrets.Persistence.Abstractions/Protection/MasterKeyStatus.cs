// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the master keys of an owner.
/// </summary>
/// <param name="Owner">The owner whose stored secrets the master keys protect.</param>
/// <param name="Keys">Every master key of the owner, newest version first.</param>
/// <param name="RecoveryConfigured">Whether new master keys are also wrapped under a recovery key.</param>
// See ADR-1407.
[PublicAPI]
public sealed record MasterKeyStatus(
    SecretOwner Owner,
    IReadOnlyList<MasterKeyInfo> Keys,
    bool RecoveryConfigured
);
