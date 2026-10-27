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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents the outcome of a token endpoint request: either a <see cref="TokenResponse"/> or a <see cref="ProtocolError"/>.
/// </summary>
[PublicAPI]
public sealed class TokenResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenResult"/> class for a successful response.
    /// </summary>
    /// <param name="response">The token response.</param>
    public TokenResult(TokenResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenResult"/> class for an error response.
    /// </summary>
    /// <param name="error">The protocol error.</param>
    public TokenResult(ProtocolError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>
    /// Gets the token response when the request succeeded; otherwise, <see langword="null"/>.
    /// </summary>
    public TokenResponse? Response { get; }

    /// <summary>
    /// Gets the protocol error when the request failed; otherwise, <see langword="null"/>.
    /// </summary>
    public ProtocolError? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Response))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Response is not null;
}
