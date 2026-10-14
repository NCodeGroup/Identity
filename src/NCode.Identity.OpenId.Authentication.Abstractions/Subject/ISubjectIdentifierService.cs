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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Projects the server-owned, stable principal identifier to the <c>sub</c> value a specific client observes, honoring
/// the client's <c>subject_type</c> setting (OpenID Connect Core 1.0 §8). For <c>public</c> clients the principal
/// identifier is returned unchanged; for <c>pairwise</c> clients a per-sector Pairwise Pseudonymous Identifier (PPID)
/// is derived so the same end-user presents a different, non-correlatable <c>sub</c> to clients in different sectors.
/// </summary>
/// <remarks>
/// The projection is applied only where the <c>sub</c> leaves the server toward a client (ID token, access token,
/// UserInfo, back-channel <c>logout_token</c>, and token introspection); the principal identifier carried in grants,
/// consent, sessions, and revocation remains the stable internal value so cross-client user operations keep working.
/// The pairwise derivation is deterministic, so the same <c>(client sector, principal)</c> pair always yields the same
/// pseudonym without any persisted mapping.
/// <para>
/// This is the <em>outbound</em> counterpart to <see cref="OpenIdContext.GetSubjectId(System.Security.Claims.ClaimsPrincipal)"/>:
/// that seam <em>extracts</em> the stable principal identifier from an inbound <c>ClaimsPrincipal</c>, whereas this
/// seam <em>projects</em> that stable identifier to the value a specific client observes. Reading a pairwise token
/// back through the extraction seam deliberately returns the pseudonym as-is (the derivation is one-way), so the two
/// seams are not inverses and neither delegates to the other.
/// </para>
/// </remarks>
[PublicAPI]
public interface ISubjectIdentifierService
{
    /// <summary>
    /// Returns the <c>sub</c> value <paramref name="openIdClient"/> observes for the principal identified by
    /// <paramref name="principalId"/>, honoring the client's <c>subject_type</c> setting.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the <c>sub</c> value is emitted to.</param>
    /// <param name="principalId">The server-owned, stable principal identifier for the end-user.</param>
    /// <returns>The <paramref name="principalId"/> unchanged for a <c>public</c> client, or the derived Pairwise
    /// Pseudonymous Identifier for a <c>pairwise</c> client.</returns>
    string ProjectSubjectId(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string principalId
    );
}
