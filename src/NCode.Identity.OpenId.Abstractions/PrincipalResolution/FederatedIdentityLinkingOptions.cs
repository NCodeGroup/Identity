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

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Contains the options used to configure the default federated-identity linking policy, which decides whether a new
/// external connection identity is attached to an existing principal.
/// </summary>
[PublicAPI]
public sealed class FederatedIdentityLinkingOptions
{
    /// <summary>
    /// Contains the default name of the claim that carries the join key (the verified email address).
    /// </summary>
    public const string DefaultJoinClaimName = "email";

    /// <summary>
    /// Contains the default name of the claim that indicates the join key is verified.
    /// </summary>
    public const string DefaultVerifiedClaimName = "email_verified";

    /// <summary>
    /// Gets or sets the name of the claim that carries the join key used to deterministically link identities (such as
    /// an email address). The default value is <see cref="DefaultJoinClaimName"/>.
    /// </summary>
    public string JoinClaimName { get; set; } = DefaultJoinClaimName;

    /// <summary>
    /// Gets or sets the name of the claim that indicates whether the join key is verified. The default value is
    /// <see cref="DefaultVerifiedClaimName"/>.
    /// </summary>
    public string VerifiedClaimName { get; set; } = DefaultVerifiedClaimName;

    /// <summary>
    /// Gets or sets a value indicating whether the join key must be verified before it is recorded and used to link
    /// identities. When <c>true</c> (the default), an unverified join key is neither recorded nor matchable, which
    /// prevents an unverified claim from becoming an account-takeover vector.
    /// </summary>
    public bool RequireVerifiedJoinKey { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether linking is explicit-only. When <c>true</c>, a verified join key is still
    /// recorded for later explicit linking, but a new identity is never automatically attached to an existing
    /// principal. The default value is <c>false</c> (deterministic auto-attach is enabled).
    /// </summary>
    public bool ExplicitOnly { get; set; }
}
