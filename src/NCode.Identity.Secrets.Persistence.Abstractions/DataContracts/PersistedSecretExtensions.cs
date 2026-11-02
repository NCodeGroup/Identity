// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Provides extension methods for <see cref="PersistedSecret"/>.
/// </summary>
// See ADR-1268.
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
    }
}
