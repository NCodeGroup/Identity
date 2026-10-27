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
/// Sends requests to a provider's token endpoint (RFC 6749 §3.2).
/// </summary>
/// <remarks>
/// An OAuth error response is returned as data in <see cref="TokenResult.Error"/>. Any other failure, such as an
/// unreachable provider, a timeout, an oversized or invalid body, or a success without <c>access_token</c>, throws
/// <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface ITokenClient
{
    /// <summary>
    /// Requests a token for the specified grant.
    /// </summary>
    /// <param name="registration">The client making the request.</param>
    /// <param name="request">The grant to request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<TokenResult> RequestTokenAsync(
        ClientRegistration registration,
        TokenRequest request,
        CancellationToken cancellationToken
    );
}
