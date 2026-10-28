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
/// Validates the logout tokens that a provider posts to a client's back-channel logout endpoint.
/// </summary>
/// <remarks>
/// An invalid token is returned as data, where <see cref="LogoutTokenValidationResult.FailureCode"/> names why it is
/// invalid and <see cref="LogoutTokenValidationResult.FailureReason"/> describes it. A failure of the provider, such as
/// an unreachable discovery or JWKS endpoint, throws <see cref="OpenIdProviderException"/>. A valid token's <c>jti</c>
/// is recorded in the shared <c>IExpiringKeySet</c>, so the same token validates only once.
/// </remarks>
[PublicAPI]
public interface ILogoutTokenValidator
{
    /// <summary>
    /// Validates a logout token that was sent to the specified client.
    /// </summary>
    /// <param name="registration">The client the logout token was sent to.</param>
    /// <param name="request">The logout token to validate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<LogoutTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        LogoutTokenValidationRequest request,
        CancellationToken cancellationToken
    );
}
