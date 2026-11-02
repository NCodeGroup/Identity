// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;

/// <summary>
/// Provides the default implementation of <see cref="ISubjectTokenValidator"/>, which accepts only tokens this
/// authorization server itself issued, validated through <see cref="IIssuedTokenValidator"/> under the policy for the
/// presented token type: an access token (<c>typ</c> <c>at+jwt</c>) for the <c>access_token</c> type, an ID token for
/// the <c>id_token</c> type, and either of them — never another JWT the server signs, such as a logout token or an
/// introspection response — for the <c>jwt</c> type. Tokens minted by external/enterprise identity providers are
/// intentionally rejected; a host that federates with upstream issuers replaces or decorates this service to accept
/// them.
/// </summary>
// See ADR-1101 and ADR-1167.
internal class DefaultSubjectTokenValidator(IIssuedTokenValidator issuedTokenValidator)
    : ISubjectTokenValidator
{
    private static AccessTokenValidationOptions AccessTokenOptions { get; } = new();

    private IIssuedTokenValidator IssuedTokenValidator { get; } = issuedTokenValidator;

    /// <inheritdoc />
    public async ValueTask<SubjectTokenValidationResult> ValidateAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string token,
        string tokenTypeIdentifier,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        // No audience constraint here: the requester-entitlement rule (RFC 8693) is enforced by the grant handler
        // after validation, which is a token-exchange-specific policy rather than a check of the token itself.
        JwtValidationResult validateResult;
        if (IsTokenType(tokenTypeIdentifier, OpenIdProtocol.TokenTypeIdentifiers.AccessToken))
        {
            validateResult = await ValidateAccessTokenAsync(
                openIdContext,
                token,
                cancellationToken
            );
        }
        else if (IsTokenType(tokenTypeIdentifier, OpenIdProtocol.TokenTypeIdentifiers.IdToken))
        {
            validateResult = await IssuedTokenValidator.ValidateIdTokenAsync(
                openIdContext,
                token,
                cancellationToken
            );
        }
        else if (IsTokenType(tokenTypeIdentifier, OpenIdProtocol.TokenTypeIdentifiers.Jwt))
        {
            // The two policies require different 'typ' values, so at most one of them accepts a token.
            validateResult = await ValidateAccessTokenAsync(
                openIdContext,
                token,
                cancellationToken
            );
            if (!validateResult.IsValid)
            {
                validateResult = await IssuedTokenValidator.ValidateIdTokenAsync(
                    openIdContext,
                    token,
                    cancellationToken
                );
            }
        }
        else
        {
            return SubjectTokenValidationResult.Fail(
                errorFactory
                    .InvalidRequest(
                        $"The token type '{tokenTypeIdentifier}' is not supported by this authorization server."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );
        }

        if (!validateResult.IsValid)
        {
            return SubjectTokenValidationResult.Fail(
                errorFactory
                    .InvalidGrant("The presented token is invalid, expired, or revoked.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );
        }

        var claimsIdentity = await validateResult.GetClaimsIdentityAsync(cancellationToken);
        var principal = new ClaimsPrincipal(claimsIdentity);

        return SubjectTokenValidationResult.Success(
            principal,
            validateResult.DecodedJwt.Payload,
            tokenTypeIdentifier
        );
    }

    private async ValueTask<JwtValidationResult> ValidateAccessTokenAsync(
        OpenIdContext openIdContext,
        string token,
        CancellationToken cancellationToken
    ) =>
        await IssuedTokenValidator.ValidateAccessTokenAsync(
            openIdContext,
            token,
            AccessTokenOptions,
            cancellationToken
        );

    private static bool IsTokenType(string tokenTypeIdentifier, string expected) =>
        string.Equals(tokenTypeIdentifier, expected, StringComparison.Ordinal);
}
