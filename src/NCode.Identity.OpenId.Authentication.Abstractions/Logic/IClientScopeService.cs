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

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Resolves the set of scope values a client is permitted to request in a tenant, derived from the tenant's resource
/// servers and the client's grants. A system resource server is implicitly available to every client; a non-system
/// resource server is available only through a client grant, intersected with the resource server's current scopes
/// (a granted scope no longer defined on the resource server is ignored). Register a custom implementation to replace
/// the default behavior.
/// </summary>
[PublicAPI]
public interface IClientScopeService
{
    /// <summary>
    /// Gets the set of scope values the specified client is permitted to request in the specified tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that owns the resource servers and the client.</param>
    /// <param name="clientId">The identifier of the client whose allowed scopes are resolved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the set of allowed
    /// scope values.</returns>
    ValueTask<IReadOnlyCollection<string>> GetAllowedScopesAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves the audiences for the specified scopes: the identifiers of the tenant's enabled resource servers that
    /// own at least one of the scopes. Used to bind an access token's <c>aud</c> to the resource servers it targets.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that owns the resource servers.</param>
    /// <param name="scopes">The scope values whose owning resource servers are resolved.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the distinct resource
    /// server identifiers (audiences) for the scopes.</returns>
    ValueTask<IReadOnlyCollection<string>> ResolveAudiencesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken
    );
}
