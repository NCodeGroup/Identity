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
