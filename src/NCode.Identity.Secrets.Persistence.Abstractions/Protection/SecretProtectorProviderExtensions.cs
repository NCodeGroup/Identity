// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides extension methods for <see cref="ISecretProtectorProvider"/>.
/// </summary>
// See ADR-1235.
[PublicAPI]
public static class SecretProtectorProviderExtensions
{
    /// <param name="provider">The <see cref="ISecretProtectorProvider"/> that provides the protector.</param>
    extension(ISecretProtectorProvider provider)
    {
        /// <summary>
        /// Gets the <see cref="ISecretProtector"/> of the specified owner, guaranteeing that every private secret in
        /// <paramref name="secrets"/> can be unprotected by the returned protector.
        /// </summary>
        /// <remarks>
        /// Secrets with a public or remote encoding are stored without protection, so they do not require a master key
        /// and are not passed to the provider.
        /// </remarks>
        /// <param name="owner">The <see cref="SecretOwner"/> whose master keys protect the secrets.</param>
        /// <param name="secrets">The <see cref="PersistedSecret"/> instances that the returned protector must be able
        /// to unprotect.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
        /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
        /// <see cref="ISecretProtector"/> of the owner.</returns>
        [PublicAPI]
        public ValueTask<ISecretProtector> GetProtectorForSecretsAsync(
            SecretOwner owner,
            IEnumerable<PersistedSecret> secrets,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(secrets);

            List<string>? protectedValues = null;
            foreach (var secret in secrets)
            {
                if (SecretEncodings.IsUnprotected(secret.SecretEncoding))
                    continue;

                // The list is created only when a private secret exists, so a call with none shares the empty array.
                protectedValues ??= new List<string>(
                    secrets.TryGetNonEnumeratedCount(out var count) ? count : 0
                );
                protectedValues.Add(secret.EncodedValue);
            }

            // A collection expression takes the type of the other operand of ??, so the cast keeps it the shared empty
            // array rather than a new list.
            return provider.GetProtectorAsync(
                owner,
                protectedValues ?? (IReadOnlyCollection<string>)[],
                cancellationToken
            );
        }
    }
}
