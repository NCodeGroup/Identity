// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the result of setting the root key of an owner.
/// </summary>
/// <param name="RootKey">The root key as stored.</param>
/// <param name="Change">The change that the audit trail records: one of the values in
/// <see cref="Auditing.RootKeyChanges"/>, or <see langword="null"/> when nothing changed.</param>
/// <param name="Rotation">The rotation that moved the owner onto the key, or <see langword="null"/> when none was
/// needed or it is deferred to the validation service.</param>
/// <param name="Rewrap">The outcome of rewrapping the owner's master keys under the key.</param>
// See ADR-1411.
[PublicAPI]
public sealed record OwnerRootKeyChange(
    OwnerRootKeyInfo RootKey,
    string? Change,
    MasterKeyRotation? Rotation,
    MasterKeyRewrapResult Rewrap
);
