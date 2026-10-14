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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Represents the proof-of-possession decision for a token request: the OAuth <c>token_type</c> to return and the
/// <c>cnf</c> confirmation to embed in the issued access token. A plain bearer token request yields
/// <see cref="Unbound"/>.
/// </summary>
[PublicAPI]
public readonly record struct ProofOfPossessionBinding
{
    /// <summary>
    /// Gets an unbound binding whose <see cref="TokenType"/> is <c>Bearer</c> and which carries no confirmation.
    /// </summary>
    public static ProofOfPossessionBinding Unbound { get; } =
        new()
        {
            TokenType = OpenIdConstants.TokenTypes.Bearer,
            Confirmation = TokenConfirmation.None,
        };

    /// <summary>
    /// Gets the OAuth 2.0 <c>token_type</c> to report in the token response (for example <c>Bearer</c> or <c>DPoP</c>).
    /// </summary>
    public required string TokenType { get; init; }

    /// <summary>
    /// Gets the <see cref="TokenConfirmation"/> to embed as the access token's <c>cnf</c> claim.
    /// </summary>
    public TokenConfirmation Confirmation { get; init; }

    /// <summary>
    /// Gets a value indicating whether the issued access token is sender-constrained (DPoP or mutual-TLS bound).
    /// </summary>
    public bool IsSenderConstrained => Confirmation.HasValue;
}
