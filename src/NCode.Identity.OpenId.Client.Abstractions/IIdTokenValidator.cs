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
/// Validates the ID tokens that a provider issues to a client.
/// </summary>
/// <remarks>
/// An invalid token is returned as data in <see cref="IdTokenValidationResult.FailureReason"/>. A failure of the
/// provider, such as an unreachable discovery or JWKS endpoint, throws <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface IIdTokenValidator
{
    /// <summary>
    /// Validates an ID token that was issued to the specified client.
    /// </summary>
    /// <param name="registration">The client the ID token was issued to.</param>
    /// <param name="request">The ID token and the values to check it against.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<IdTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        IdTokenValidationRequest request,
        CancellationToken cancellationToken
    );
}
