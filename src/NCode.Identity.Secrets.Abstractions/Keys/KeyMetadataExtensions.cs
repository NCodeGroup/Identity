// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides extension methods for <see cref="KeyMetadata"/> that judge a key's lifecycle at an instant: whether it is
/// in its active window, when it signs or encrypts, and whether it has expired, after which it neither verifies nor
/// decrypts.
/// </summary>
// See ADR-1403.
[PublicAPI]
public static class KeyMetadataExtensions
{
    /// <param name="metadata">The <see cref="KeyMetadata"/> to judge.</param>
    extension(KeyMetadata metadata)
    {
        /// <summary>
        /// Determines whether the key is in its active window at <paramref name="now"/>: its activation, when it has
        /// one, has passed, and neither its retirement nor its expiry, when it has them, has.
        /// </summary>
        /// <param name="now">The instant to judge the key at.</param>
        /// <returns><c>true</c> when the key signs or encrypts at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
        [PublicAPI]
        public bool IsActive(DateTimeOffset now) =>
            KeyLifecycle.IsActive(
                metadata.ActivatesWhen,
                metadata.RetiresWhen,
                metadata.ExpiresWhen,
                now
            );

        /// <summary>
        /// Determines whether the key has expired at <paramref name="now"/>: its expiry, when it has one, has passed.
        /// </summary>
        /// <param name="now">The instant to judge the key at.</param>
        /// <returns><c>true</c> when the key neither verifies nor decrypts at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
        [PublicAPI]
        public bool IsExpired(DateTimeOffset now) =>
            KeyLifecycle.IsExpired(metadata.ExpiresWhen, now);
    }
}
