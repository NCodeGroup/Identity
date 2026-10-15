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
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Resolves the effective set of verification keys for a client: its stored <see cref="OpenIdClient.SecretKeys"/>
/// merged with the public keys resolved at runtime from the client's <c>jwks_uri</c> setting (when configured). The
/// merged collection is consumed by the paths that verify a client-signed JWT against the client's keys —
/// <c>private_key_jwt</c> client authentication and signed request objects (JAR).
/// </summary>
[PublicAPI]
public interface IClientKeyResolver
{
    /// <summary>
    /// Resolves the effective verification-key collection for the specified client.
    /// </summary>
    /// <param name="openIdClient">The client whose keys to resolve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> containing the merged <see cref="ISecretKeyCollection"/>. When the client has
    /// no <c>jwks_uri</c> configured (or it cannot be resolved), the client's stored keys are returned unchanged.</returns>
    ValueTask<ISecretKeyCollection> ResolveSecretKeysAsync(
        OpenIdClient openIdClient,
        CancellationToken cancellationToken
    );
}
