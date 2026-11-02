// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Resolves and caches the public signing keys published by the provider at its <c>jwks_uri</c>.
/// </summary>
/// <remarks>
/// Keys the client cannot use are skipped, but a set that carries private or symmetric material, duplicate
/// <c>kid</c> values, or no usable key is refused (RFC 7517 §5).
/// </remarks>
[PublicAPI]
public interface IProviderKeySetResolver
{
    /// <summary>
    /// Gets the signing keys of the specified authority, from the cache when they are fresh.
    /// </summary>
    /// <param name="authority">The issuer identifier of the provider.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<ISecretKeyCollection> GetSigningKeysAsync(
        string authority,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Forces a refresh of the signing keys, for example after an unknown <c>kid</c>. The refresh is rate-limited, so
    /// a request inside the minimum refresh interval returns the cached keys.
    /// </summary>
    /// <param name="authority">The issuer identifier of the provider.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<ISecretKeyCollection> RefreshSigningKeysAsync(
        string authority,
        CancellationToken cancellationToken
    );
}
