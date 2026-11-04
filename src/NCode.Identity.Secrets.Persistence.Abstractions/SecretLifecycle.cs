// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// The one rule for the instants of a secret's lifecycle: a secret is created, activates, retires and expires in that
/// order, so its active window (activation to retirement) lies within its validity window (creation to expiry). A
/// request that names no activation activates the secret when it is created, and one that names no retirement retires
/// it when it expires.
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
    )
    {
        var resolvedActivatesWhen = activatesWhen ?? createdWhen;
        var resolvedRetiresWhen = retiresWhen ?? expiresWhen;

        if (
            !TryValidate(
                createdWhen,
                resolvedActivatesWhen,
                resolvedRetiresWhen,
                expiresWhen,
                out var error
            )
        )
        {
            throw new ArgumentException(error);
        }

        return (resolvedActivatesWhen, resolvedRetiresWhen);
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
