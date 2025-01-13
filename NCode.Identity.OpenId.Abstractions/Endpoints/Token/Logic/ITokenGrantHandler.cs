#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Endpoints.Token.Logic;

/// <summary>
/// Implemented by various handlers to process token requests for a specific grant type.
/// </summary>
[PublicAPI]
public interface ITokenGrantHandler
{
    /// <summary>
    /// Gets the type of grants that this <see cref="ITokenGrantHandler"/> instance supports.
    /// </summary>
    IReadOnlySet<string> GrantTypes { get; }

    /// <summary>
    /// Handles the token request for the specified <see cref="ITokenRequest"/> by returning the appropriate HTTP response.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="tokenRequest">The <see cref="ITokenRequest"/> that represents the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="IOpenIdResponse"/>.</returns>
    ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        CancellationToken cancellationToken);
}
