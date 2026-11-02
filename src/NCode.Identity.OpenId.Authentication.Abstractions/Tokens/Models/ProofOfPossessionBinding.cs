// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
            TokenType = OpenIdProtocol.TokenTypes.Bearer,
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
