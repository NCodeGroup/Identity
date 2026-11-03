// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides extension methods for <see cref="ISecretSerializer"/>.
/// </summary>
[PublicAPI]
public static class SecretSerializerExtensions
{
    /// <param name="serializer">The <see cref="ISecretSerializer"/> that reads the secret.</param>
    extension(ISecretSerializer serializer)
    {
        /// <summary>
        /// Reads the public half of a secret, as <see cref="ISecretSerializer.DeserializePublicSecret"/> does, and reports
        /// a secret whose public half cannot be read instead of throwing: one that holds no public key material, whose
        /// secret type is not registered, or whose stored public key is malformed or of a form its key type does not
        /// read.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="persistedSecret"/> is <c>null</c>.</exception>
        /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose public half is read.</param>
        /// <param name="publicKey">When this method returns <c>true</c>, the public half of the secret.</param>
        /// <param name="error">When this method returns <c>false</c>, the reason the public half cannot be read.</param>
        /// <returns><c>true</c> when the public half was read; otherwise, <c>false</c>.</returns>
        public bool TryDeserializePublicSecret(
            PersistedSecret persistedSecret,
            [NotNullWhen(true)] out SecretKey? publicKey,
            [NotNullWhen(false)] out string? error
        )
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(persistedSecret);

            try
            {
                publicKey = serializer.DeserializePublicSecret(persistedSecret);
                error = null;
                return true;
            }
            catch (Exception exception)
                when (exception
                        is ArgumentException
                            or InvalidOperationException
                            or NotSupportedException
                            or CryptographicException
                            or FormatException
                )
            {
                publicKey = null;
                error = exception.Message;
                return false;
            }
        }
    }
}
