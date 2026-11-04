// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Finds the values that master keys protect, so that a rotation can rewrap them and a retired master key is destroyed
/// only once none of them names it. A package that protects its own values with <see cref="ISecretProtector"/>
/// registers a source for them: a value that no source reports is not rewrapped, and becomes unreadable once the master
/// key that protects it is destroyed.
/// </summary>
/// <remarks>
/// A source ignores an owner that it holds nothing for. Sources are registered as a collection, and every source is
/// asked about every retired key.
/// </remarks>
// See ADR-1407.
[PublicAPI]
public interface IProtectedValueSource
{
    /// <summary>
    /// Rewraps every value of the source that a specific master key protects under the active master key of the owner.
    /// The call is idempotent: a rewrapped value no longer names the key.
    /// </summary>
    /// <param name="owner">The owner whose values are rewrapped.</param>
    /// <param name="masterKeyId">The identifier of the master key whose values are rewrapped.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of values
    /// that were rewrapped, or zero when the source holds nothing for the owner.</returns>
    ValueTask<int> RewrapAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether any value of the source is still protected by a specific master key.
    /// </summary>
    /// <param name="owner">The owner whose values are examined.</param>
    /// <param name="masterKeyId">The identifier of the master key.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing
    /// <see langword="true"/> when a value names the key, or <see langword="false"/> when none does or the source holds
    /// nothing for the owner.</returns>
    ValueTask<bool> ReferencesAsync(
        SecretOwner owner,
        Guid masterKeyId,
        CancellationToken cancellationToken
    );
}
