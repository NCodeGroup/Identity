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

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Supports OpenID Connect Session Management 1.0: it maintains the OP browser-state cookie and derives the
/// <c>session_state</c> value returned in the authorization response and recomputed by the <c>check_session_iframe</c>.
/// Session Management is available but disabled by default (it depends on third-party cookies the browser platform is
/// removing); the <c>session_management_supported</c> setting gates its use.
/// </summary>
[PublicAPI]
public interface ISessionStateService
{
    /// <summary>
    /// Ensures the OP browser-state cookie exists on the current response (creating an opaque value when absent) and
    /// computes the <c>session_state</c> value for the specified client and its redirect URI origin.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="clientId">The <c>client_id</c> of the relying party the authorization response is being sent to.</param>
    /// <param name="clientRedirectUri">The validated <c>redirect_uri</c> whose origin salts the <c>session_state</c>.</param>
    /// <returns>The computed <c>session_state</c> value.</returns>
    string CreateSessionState(OpenIdContext openIdContext, string clientId, Uri clientRedirectUri);

    /// <summary>
    /// Removes the OP browser-state cookie so that relying parties polling the <c>check_session_iframe</c> observe the
    /// login-state change. Invoked when the OP login is cleared (logout).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    void ClearBrowserState(OpenIdContext openIdContext);
}
