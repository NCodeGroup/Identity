// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// The one rule for the instants of a secret's lifecycle: a secret is created, activates, retires and expires in that
/// order, so its active window (activation to retirement) lies within its validity window (creation to expiry). A
/// request that creates a secret and names no activation activates it when it is created, and one that names no
/// retirement retires it when it expires; a request that updates a secret and omits an instant keeps the stored one,
/// the retirement bounded by the new expiry.
/// </summary>
// See ADR-1403.
[PublicAPI]
public static class SecretLifecycle
{
    /// <summary>
    /// Resolves the instants a secret activates and retires from a request's optional values and checks their order
    /// against the secret's creation and expiry.
    /// </summary>
    /// <param name="createdWhen">The instant the secret is created.</param>
    /// <param name="activatesWhen">The instant the secret activates, or <c>null</c> for <paramref name="createdWhen"/>.</param>
    /// <param name="retiresWhen">The instant the secret retires, or <c>null</c> for <paramref name="expiresWhen"/>.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <returns>The instants the secret activates and retires.</returns>
    /// <exception cref="ArgumentException">The instants are out of order.</exception>
    public static (DateTimeOffset ActivatesWhen, DateTimeOffset RetiresWhen) Resolve(
        DateTimeOffset createdWhen,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        TryResolve(
            createdWhen,
            activatesWhen,
            retiresWhen,
            expiresWhen,
            out var resolved,
            out var error
        )
            ? resolved
            : throw new ArgumentException(error);

    /// <summary>
    /// Resolves the instants a secret activates and retires from a request's optional values, as
    /// <see cref="Resolve"/> does, and reports instead of throwing when they are out of order.
    /// </summary>
    /// <param name="createdWhen">The instant the secret is created.</param>
    /// <param name="activatesWhen">The instant the secret activates, or <c>null</c> for <paramref name="createdWhen"/>.</param>
    /// <param name="retiresWhen">The instant the secret retires, or <c>null</c> for <paramref name="expiresWhen"/>.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <param name="resolved">The instants the secret activates and retires, in order or not.</param>
    /// <param name="error">When this method returns <c>false</c>, a message that names the instants out of order.</param>
    /// <returns><c>true</c> when the instants are in order; otherwise, <c>false</c>.</returns>
    public static bool TryResolve(
        DateTimeOffset createdWhen,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen,
        out (DateTimeOffset ActivatesWhen, DateTimeOffset RetiresWhen) resolved,
        [NotNullWhen(false)] out string? error
    )
    {
        resolved = (activatesWhen ?? createdWhen, retiresWhen ?? expiresWhen);

        return TryValidate(
            createdWhen,
            resolved.ActivatesWhen,
            resolved.RetiresWhen,
            expiresWhen,
            out error
        );
    }

    /// <summary>
    /// Resolves the instants a stored secret activates and retires from an update's optional values and checks their
    /// order against the secret's creation and the new expiry. An instant the update omits keeps the stored one, and
    /// a stored retirement later than the new expiry becomes the expiry, as a rotation that shortens a secret's
    /// validity bounds its active window with it; a retired or upcoming secret therefore stays so unless the update
    /// names the instant.
    /// </summary>
    /// <param name="secret">The stored secret the update applies to.</param>
    /// <param name="activatesWhen">The instant the secret activates, or <c>null</c> for the stored one.</param>
    /// <param name="retiresWhen">The instant the secret retires, or <c>null</c> for the stored one, bounded by
    /// <paramref name="expiresWhen"/>.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <returns>The instants the secret activates and retires.</returns>
    /// <exception cref="ArgumentException">The instants are out of order.</exception>
    public static (DateTimeOffset ActivatesWhen, DateTimeOffset RetiresWhen) ResolveUpdate(
        PersistedSecret secret,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen
    ) =>
        TryResolveUpdate(
            secret,
            activatesWhen,
            retiresWhen,
            expiresWhen,
            out var resolved,
            out var error
        )
            ? resolved
            : throw new ArgumentException(error);

    /// <summary>
    /// Resolves the instants a stored secret activates and retires from an update's optional values, as
    /// <see cref="ResolveUpdate"/> does, and reports instead of throwing when they are out of order.
    /// </summary>
    /// <param name="secret">The stored secret the update applies to.</param>
    /// <param name="activatesWhen">The instant the secret activates, or <c>null</c> for the stored one.</param>
    /// <param name="retiresWhen">The instant the secret retires, or <c>null</c> for the stored one, bounded by
    /// <paramref name="expiresWhen"/>.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <param name="resolved">The instants the secret activates and retires, in order or not.</param>
    /// <param name="error">When this method returns <c>false</c>, a message that names the instants out of order.</param>
    /// <returns><c>true</c> when the instants are in order; otherwise, <c>false</c>.</returns>
    public static bool TryResolveUpdate(
        PersistedSecret secret,
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset expiresWhen,
        out (DateTimeOffset ActivatesWhen, DateTimeOffset RetiresWhen) resolved,
        [NotNullWhen(false)] out string? error
    )
    {
        ArgumentNullException.ThrowIfNull(secret);

        resolved = (
            activatesWhen ?? secret.ActivatesWhen,
            retiresWhen ?? (secret.RetiresWhen < expiresWhen ? secret.RetiresWhen : expiresWhen)
        );

        return TryValidate(
            secret.CreatedWhen,
            resolved.ActivatesWhen,
            resolved.RetiresWhen,
            expiresWhen,
            out error
        );
    }

    /// <summary>
    /// Determines whether the instants of a secret's lifecycle are in order: created, then activates, then retires,
    /// then expires, where two neighbors may coincide.
    /// </summary>
    /// <param name="createdWhen">The instant the secret is created.</param>
    /// <param name="activatesWhen">The instant the secret activates.</param>
    /// <param name="retiresWhen">The instant the secret retires.</param>
    /// <param name="expiresWhen">The instant the secret expires.</param>
    /// <param name="error">When this method returns <c>false</c>, a message that names the instants out of order.</param>
    /// <returns><c>true</c> when the instants are in order; otherwise, <c>false</c>.</returns>
    public static bool TryValidate(
        DateTimeOffset createdWhen,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset expiresWhen,
        [NotNullWhen(false)] out string? error
    )
    {
        if (activatesWhen < createdWhen)
        {
            error =
                $"The secret cannot activate ({activatesWhen:O}) before it is created ({createdWhen:O}).";
            return false;
        }

        if (retiresWhen < activatesWhen)
        {
            error =
                $"The secret cannot retire ({retiresWhen:O}) before it activates ({activatesWhen:O}).";
            return false;
        }

        if (expiresWhen < retiresWhen)
        {
            error =
                $"The secret cannot expire ({expiresWhen:O}) before it retires ({retiresWhen:O}).";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Determines whether the instants of a persisted secret's lifecycle are in order, as
    /// <see cref="TryValidate(DateTimeOffset, DateTimeOffset, DateTimeOffset, DateTimeOffset, out string)"/> judges
    /// them.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> to check.</param>
    /// <param name="error">When this method returns <c>false</c>, a message that names the instants out of order.</param>
    /// <returns><c>true</c> when the instants are in order; otherwise, <c>false</c>.</returns>
    public static bool TryValidate(PersistedSecret secret, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return TryValidate(
            secret.CreatedWhen,
            secret.ActivatesWhen,
            secret.RetiresWhen,
            secret.ExpiresWhen,
            out error
        );
    }
}
