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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents a newly minted initial access token (RFC 7591). The <see cref="InitialAccessToken"/> value is revealed
/// exactly once at creation and never again, because the grant store persists only its salted hash.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreatedInitialAccessTokenResource
{
    /// <summary>
    /// Gets the one-time plaintext initial access token value. The bearer presents it to the registration endpoint; it
    /// is never retrievable again and should be delivered to the registrant securely.
    /// </summary>
    public required string InitialAccessToken { get; init; }

    /// <summary>
    /// Gets the opaque, server-generated public identifier of the persisted grant backing the token, used to revoke it
    /// through the grant management endpoint.
    /// </summary>
    public required string GrantId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that owns the token.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the optional human-readable description recorded with the token.
    /// </summary>
    public required string? Description { get; init; }

    /// <summary>
    /// Gets when the token was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets when the token expires, or <c>null</c> when it does not expire.
    /// </summary>
    public required DateTimeOffset? ExpiresWhen { get; init; }
}
