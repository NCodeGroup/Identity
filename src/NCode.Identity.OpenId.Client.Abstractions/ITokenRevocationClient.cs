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
/// Sends requests to a provider's revocation endpoint (RFC 7009).
/// </summary>
/// <remarks>
/// An OAuth error response is returned as data. Any other failure throws <see cref="OpenIdProviderException"/>, and a
/// provider without a <c>revocation_endpoint</c> throws <see cref="InvalidOperationException"/>.
/// </remarks>
[PublicAPI]
public interface ITokenRevocationClient
{
    /// <summary>
    /// Revokes a token.
    /// </summary>
    /// <param name="registration">The client making the request.</param>
    /// <param name="request">The token to revoke.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns><c>null</c> on success, or the error the provider returned.</returns>
    ValueTask<ProtocolError?> RevokeTokenAsync(
        ClientRegistration registration,
        TokenRevocationRequest request,
        CancellationToken cancellationToken
    );
}
