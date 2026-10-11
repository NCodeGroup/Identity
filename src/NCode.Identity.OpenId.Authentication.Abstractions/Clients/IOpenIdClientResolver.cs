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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Resolves the registered <see cref="OpenIdClient"/> for a given <c>client_id</c> within the current request's tenant.
/// This is front-channel client <em>identification</em> — loading the enabled client and its effective settings and
/// secrets — as distinct from credential-based client <em>authentication</em>. It is the single resolution path shared
/// by the authorization and continuation endpoints (which identify a client without credentials) and by the first step
/// of client authentication (which then verifies a credential against the resolved client).
/// </summary>
[PublicAPI]
public interface IOpenIdClientResolver
{
    /// <summary>
    /// Resolves the enabled public client registered under <paramref name="clientId"/> within the current request's
    /// tenant, returning <see langword="null"/> when no enabled client matches (absent, disabled, or belonging to a
    /// different tenant).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="clientId">The client identifier to resolve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous
    /// operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// <see cref="OpenIdClient"/>, or <see langword="null"/> when no enabled client matches.</returns>
    ValueTask<OpenIdClient?> ResolveClientOrDefaultAsync(
        OpenIdContext openIdContext,
        string clientId,
        CancellationToken cancellationToken
    );
}
