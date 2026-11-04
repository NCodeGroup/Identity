// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Reports the result of a rotation: the key that became active and the key that it retired.
/// </summary>
/// <param name="Owner">The owner whose master key rotated.</param>
/// <param name="ActiveKey">The master key that is now active.</param>
/// <param name="RetiredKey">The master key that the rotation retired, or <see langword="null"/> when the owner had
/// no active key.</param>
// See ADR-1407.
[PublicAPI]
public sealed record MasterKeyRotation(
    SecretOwner Owner,
    MasterKeyInfo ActiveKey,
    MasterKeyInfo? RetiredKey
);
