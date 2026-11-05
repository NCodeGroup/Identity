// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the result of removing the root key of an owner.
/// </summary>
/// <param name="Remaining">The root key while master keys remain under it, in the state
/// <see cref="OwnerRootKeyStates.Removing"/>, or <see langword="null"/> once the record is deleted.</param>
/// <param name="Removed"><see langword="true"/> when this call moved the key to the removing state.</param>
/// <param name="Rewrap">The outcome of moving the owner's master keys under the deployment's root key.</param>
// See ADR-1414.
[PublicAPI]
public sealed record OwnerRootKeyRemoval(
    OwnerRootKeyInfo? Remaining,
    bool Removed,
    MasterKeyRewrapResult Rewrap
);
