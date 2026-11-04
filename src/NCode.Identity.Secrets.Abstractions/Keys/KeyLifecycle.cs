// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// The one rule that judges a key's windows at an instant, shared by every record that carries the instants: the
/// active window, from the activation to the retirement, is when a key signs or encrypts; the validity window, which
/// closes at the expiry, is when it verifies, decrypts and is published. An instant a record does not carry is no
/// bound.
/// </summary>
// See ADR-1403.
[PublicAPI]
public static class KeyLifecycle
{
    /// <summary>
    /// Determines whether a key is in its active window at <paramref name="now"/>: its activation, when it has one,
    /// has passed, and neither its retirement nor its expiry, when it has them, has.
    /// </summary>
    /// <param name="activatesWhen">The instant the key activates, or <c>null</c> when it has none.</param>
    /// <param name="retiresWhen">The instant the key retires, or <c>null</c> when it has none.</param>
    /// <param name="expiresWhen">The instant the key expires, or <c>null</c> when it has none.</param>
    /// <param name="now">The instant to judge the key at.</param>
    /// <returns><c>true</c> when the key signs or encrypts at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
    public static bool IsActive(
        DateTimeOffset? activatesWhen,
        DateTimeOffset? retiresWhen,
        DateTimeOffset? expiresWhen,
        DateTimeOffset now
    ) =>
        (activatesWhen is not { } activates || activates <= now)
        && (retiresWhen is not { } retires || retires > now)
        && !IsExpired(expiresWhen, now);

    /// <summary>
    /// Determines whether a key has expired at <paramref name="now"/>: its expiry, when it has one, has passed.
    /// </summary>
    /// <param name="expiresWhen">The instant the key expires, or <c>null</c> when it has none.</param>
    /// <param name="now">The instant to judge the key at.</param>
    /// <returns><c>true</c> when the key neither verifies nor decrypts at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
    public static bool IsExpired(DateTimeOffset? expiresWhen, DateTimeOffset now) =>
        expiresWhen is { } expires && expires <= now;
}
