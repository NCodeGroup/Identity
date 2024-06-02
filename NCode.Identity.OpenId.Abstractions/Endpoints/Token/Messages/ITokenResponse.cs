#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Endpoints.Token.Messages;

public interface ITokenResponse : IOpenIdMessage
{
    /// <summary>
    /// Gets or sets the <c>access_token</c> parameter.
    /// </summary>
    string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>expires_in</c> parameter.
    /// </summary>
    TimeSpan? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token</c> parameter.
    /// </summary>
    string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>refresh_token</c> parameter.
    /// </summary>
    string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>scope</c> parameter.
    /// </summary>
    IReadOnlyCollection<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the <c>token_type</c> parameter.
    /// </summary>
    string? TokenType { get; set; }
}
