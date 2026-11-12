// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Builds the access that a referenced key wrapper uses to reach the owner root key of an owner.
/// </summary>
/// <remarks>
/// The binding of an access is the normalized <c>type:id</c> of the owner, such as <c>tenant:acme</c>. The trust of an
/// identity can require the binding, as the trust policy of an AWS role requires it as the external ID, so one owner
/// cannot make the server take on an identity that was granted to another owner.
/// </remarks>
// See ADR-1414.
internal static class OwnerRootKeyAccess
{
    /// <summary>
    /// Gets the binding of an owner: its normalized <c>type:id</c>.
    /// </summary>
    /// <param name="owner">The owner of the root key.</param>
    /// <returns>The binding that the trust of the identity can require.</returns>
    // See ADR-1414.
    public static string GetBinding(SecretOwner owner) => owner.Normalize().ToString();

    /// <summary>
    /// Gets the access through which the server reaches the owner root key of an owner.
    /// </summary>
    /// <param name="owner">The owner of the root key.</param>
    /// <param name="accessIdentity">The identity that the owner grants, or <see langword="null"/> when the server
    /// reaches the key as itself.</param>
    /// <returns>
    /// The access, or <see langword="null"/> when <paramref name="accessIdentity"/> is <see langword="null"/>.
    /// </returns>
    // See ADR-1414.
    public static KeyAccess? For(SecretOwner owner, string? accessIdentity) =>
        accessIdentity is null ? null : new KeyAccess(accessIdentity, GetBinding(owner));
}
