#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
