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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// Represents a resource server resolved at access-token issuance as one of the audiences a token targets, carrying the
/// effective settings that bound the access tokens issued for it and its own secret keys.
/// </summary>
// See ADR-0075 and ADR-0154.
[PublicAPI]
public sealed class ResolvedResourceServer
{
    /// <summary>
    /// Gets the opaque identifier of the resource server.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the audience identifier of the resource server (its <c>aud</c> value).
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the resource server's effective settings — the server baseline merged with the tenant's and then the
    /// resource server's persisted overrides. The settings among them that apply to resource servers bound every access
    /// token issued for it: they are combined with the requesting client's effective settings, and with those of every
    /// other resource server the token targets, by their lattice merges.
    /// </summary>
    public required IReadOnlySettingCollection EffectiveSettings { get; init; }

    /// <summary>
    /// Gets the provider of the resource server's own secret keys — the keys an access token can be encrypted to so
    /// that only this resource server can read it. The provider is owned by the
    /// <see cref="ResourceServerCredentialResolution"/> that carries this resource server and is released when the
    /// resolution is disposed. Access tokens are never signed with a resource server's key.
    /// </summary>
    public required ISecretKeyCollectionProvider SecretsProvider { get; init; }
}
