#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Results;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization response.
/// </summary>
public interface IAuthorizationResult : IOpenIdResult, IOpenIdMessage
{
    /// <summary>
    /// Gets or sets the <see cref="Uri"/> where the authorization response should be sent to.
    /// </summary>
    Uri RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the mechanism that should be used for sending the authorization response.
    /// </summary>
    ResponseMode ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets an <see cref="IOpenIdError"/> that contains information about the failure of the operation.
    /// </summary>
    IOpenIdError? Error { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }

    /// <summary>
    /// Gets or sets the <c>code</c> parameter.
    /// </summary>
    string? Code { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token</c> parameter.
    /// </summary>
    string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>access_token</c> parameter.
    /// </summary>
    string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>token_type</c> parameter.
    /// </summary>
    string? TokenType { get; set; }

    /// <summary>
    /// Gets or sets the <c>expires_in</c> parameter.
    /// </summary>
    TimeSpan? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the <c>iss</c> parameter.
    /// </summary>
    string? Issuer { get; set; }
}
