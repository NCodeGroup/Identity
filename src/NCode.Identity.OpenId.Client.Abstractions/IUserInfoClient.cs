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
/// Requests the claims about the end-user from the provider's UserInfo endpoint (OpenID Connect Core 1.0 §5.3) with an
/// access token, the way the token was issued: as a bearer token, or with a DPoP proof (RFC 9449 §7), over mutual TLS
/// when the client authenticates with it (RFC 8705 §5).
/// </summary>
// See ADR-0299.
[PublicAPI]
public interface IUserInfoClient
{
    /// <summary>
    /// Requests the UserInfo endpoint and verifies that the response is about the expected subject (OpenID Connect Core
    /// 1.0 §5.3.2).
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="request">The UserInfo request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The claims about the end-user, the protocol error, or the reason the response is invalid.</returns>
    /// <exception cref="InvalidOperationException">The provider does not advertise a UserInfo endpoint, or the access
    /// token is DPoP-bound and the client cannot create a DPoP proof with its key.</exception>
    /// <exception cref="OpenIdProviderException">The provider cannot be reached, or answers with neither claims nor an
    /// error.</exception>
    ValueTask<UserInfoResult> GetUserInfoAsync(
        ClientRegistration registration,
        UserInfoRequest request,
        CancellationToken cancellationToken
    );
}
