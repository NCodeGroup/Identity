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
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// Resolves the resource servers an access token targets — the tenant's enabled resource servers that own at least one
/// of the token's effective scopes — and materializes the per-audience encryption material consumed at token issuance.
/// When exactly one resource server is targeted, its effective settings and own secret keys govern the
/// token's encryption; the token is always signed with the tenant's key. Register a custom implementation to replace
/// the default behavior.
/// </summary>
// See ADR-0075.
[PublicAPI]
public interface IResourceServerCredentialResolver
{
    /// <summary>
    /// Resolves the resource servers that own at least one of the specified effective scopes within the current
    /// tenant, returning their audiences and — when exactly one is targeted — its own secret-key provider. When
    /// <paramref name="requestedResources"/> is non-empty (RFC 8707 Resource Indicators), the result is narrowed to
    /// those resource servers; a requested resource that does not correspond to an enabled resource server owning one
    /// of the effective scopes is rejected with an <c>invalid_target</c> error.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="effectiveScopes">The effective scopes the access token is being issued for.</param>
    /// <param name="requestedResources">The <c>resource</c> indicators (RFC 8707) that narrow the token's audience, or
    /// an empty collection to target every resource server owning an effective scope.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="ResourceServerCredentialResolution"/>. The caller owns the returned resolution and must dispose it.</returns>
    ValueTask<ResourceServerCredentialResolution> ResolveAsync(
        OpenIdContext openIdContext,
        IReadOnlyCollection<string> effectiveScopes,
        IReadOnlyCollection<string> requestedResources,
        CancellationToken cancellationToken
    );
}
