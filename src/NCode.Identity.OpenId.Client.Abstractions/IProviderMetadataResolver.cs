// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Resolves and caches the OpenID Connect discovery metadata of an authority.
/// </summary>
/// <remarks>
/// The metadata is fetched from <c>&lt;authority&gt;/.well-known/openid-configuration</c>. The returned <c>issuer</c>
/// must equal the authority, and every endpoint must be <c>https</c> (or <c>http</c> on a loopback host); a violation
/// is an <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface IProviderMetadataResolver
{
    /// <summary>
    /// Gets the validated metadata of the specified authority, from the cache when it is fresh.
    /// </summary>
    /// <param name="authority">The issuer identifier of the provider.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<OpenIdProviderMetadata> GetMetadataAsync(
        string authority,
        CancellationToken cancellationToken
    );
}
