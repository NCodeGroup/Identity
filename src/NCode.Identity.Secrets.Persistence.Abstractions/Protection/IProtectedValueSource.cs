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
/// asked once per owner with the set of keys.
/// </remarks>
// See ADR-1407.
[PublicAPI]
public interface IProtectedValueSource
{
    /// <summary>
    /// Rewraps, under the active master key of the owner, every value of the source that one of the master keys
    /// protects, in one pass over the source. A value that cannot be rewrapped is counted failed and the pass continues.
    /// The call is idempotent: a rewrapped value names none of the keys.
    /// </summary>
    /// <param name="owner">The owner whose values are rewrapped.</param>
    /// <param name="masterKeyIds">The identifiers of the master keys whose values are rewrapped.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of values
    /// that were rewrapped and the number that failed, or <see cref="ProtectedValueRewrapResult.None"/> when the source
    /// holds nothing for the owner.</returns>
    ValueTask<ProtectedValueRewrapResult> RewrapAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Finds the master keys that a value of the source still names.
    /// </summary>
    /// <param name="owner">The owner whose values are examined.</param>
    /// <param name="masterKeyIds">The identifiers of the master keys to look for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the subset of the
    /// master keys that a value of the source still names, found in one pass that ends once every key is found; an
    /// empty set when the source holds nothing for the owner.</returns>
    ValueTask<IReadOnlySet<Guid>> FindReferencedAsync(
        SecretOwner owner,
        IReadOnlySet<Guid> masterKeyIds,
        CancellationToken cancellationToken
    );
}
