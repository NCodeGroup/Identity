// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Decides which owners can bring a root key of their own.
/// </summary>
/// <remarks>
/// Every registered policy is consulted, and an owner can bring a root key only when none of them refuses it. A policy
/// governs the setting of a root key only: an owner that already has one keeps it. Register an implementation as a
/// collection to refuse further owners.
/// </remarks>
// See ADR-1411.
[PublicAPI]
public interface IOwnerRootKeyPolicy
{
    /// <summary>
    /// Checks whether an owner can bring a root key.
    /// </summary>
    /// <param name="owner">The owner to check, in its normalized form.</param>
    /// <param name="reason">
    /// When this method returns <see langword="false"/>, a sentence that describes why the owner cannot bring a root
    /// key; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the owner can bring a root key; otherwise, <see langword="false"/>.</returns>
    bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason);
}
