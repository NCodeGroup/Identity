// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Provides extension methods for <see cref="PersistedSecret"/>: whether its public key is at hand, and where it stands
/// in its lifecycle at an instant.
/// </summary>
// See ADR-1268 and ADR-1403.
[PublicAPI]
public static class PersistedSecretExtensions
{
    /// <param name="secret">The <see cref="PersistedSecret"/> to inspect.</param>
    extension(PersistedSecret secret)
    {
        /// <summary>
        /// Determines whether the secret's public key can be read without a protector.
        /// </summary>
        /// <returns><c>true</c> when the public key can be read without a protector; otherwise, <c>false</c>.</returns>
        [PublicAPI]
        public bool HasPublicKey()
        {
            ArgumentNullException.ThrowIfNull(secret);

            return SecretEncodings.IsPublic(secret.SecretEncoding) || secret.PublicKey is not null;
        }

        /// <summary>
        /// Determines whether the secret is in its active window at <paramref name="now"/>: its activation has passed
        /// and neither its retirement nor its expiry has, so it signs or encrypts. The one rule that judges a key's
        /// metadata judges the stored secret, so the two never disagree.
        /// </summary>
        /// <param name="now">The instant to judge the secret at.</param>
        /// <returns><c>true</c> when the secret signs or encrypts at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
        // See ADR-1403.
        [PublicAPI]
        public bool IsActive(DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(secret);

            return KeyLifecycle.IsActive(
                secret.ActivatesWhen,
                secret.RetiresWhen,
                secret.ExpiresWhen,
                now
            );
        }

        /// <summary>
        /// Determines whether the secret has expired at <paramref name="now"/>: its validity window has closed, so it
        /// neither verifies nor decrypts, is not published, and does not authenticate.
        /// </summary>
        /// <param name="now">The instant to judge the secret at.</param>
        /// <returns><c>true</c> when the secret has expired at <paramref name="now"/>; otherwise, <c>false</c>.</returns>
        // See ADR-1403.
        [PublicAPI]
        public bool IsExpired(DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(secret);

            return KeyLifecycle.IsExpired(secret.ExpiresWhen, now);
        }

        /// <summary>
        /// Gets the secret's lifecycle state at <paramref name="now"/>: one of the values in
        /// <see cref="SecretLifecycleStates"/>, judged from its activation, retirement and expiry.
        /// </summary>
        /// <param name="now">The instant to judge the secret at.</param>
        /// <returns>The name of the secret's lifecycle state at <paramref name="now"/>.</returns>
        // See ADR-1403.
        [PublicAPI]
        public string GetLifecycleState(DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(secret);

            if (secret.IsExpired(now))
                return SecretLifecycleStates.Expired;

            if (secret.RetiresWhen <= now)
                return SecretLifecycleStates.Retired;

            return secret.ActivatesWhen <= now
                ? SecretLifecycleStates.Active
                : SecretLifecycleStates.Upcoming;
        }
    }
}
