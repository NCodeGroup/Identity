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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;

/// <summary>
/// Provides the default implementation of <see cref="ISubjectTokenValidator"/>, which accepts only tokens this
/// authorization server itself issued: a self-issued JWT validated against the resolved tenant's signing keys, issuer,
/// and lifetime. Tokens minted by external/enterprise identity providers are intentionally rejected; a host that
/// federates with upstream issuers replaces or decorates this service to accept them.
/// </summary>
internal class DefaultSubjectTokenValidator(IJsonWebTokenService jsonWebTokenService)
    : ISubjectTokenValidator
{
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;

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

        if (!IsJwtTokenType(tokenTypeIdentifier))
        {
            return SubjectTokenValidationResult.Fail(
                errorFactory
                    .InvalidRequest(
                        $"The token type '{tokenTypeIdentifier}' is not supported by this authorization server."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );
        }

        var openIdTenant = openIdContext.Tenant;

        ValidateJwtResult validateResult;
        try
        {
            // No audience constraint here: the requester-entitlement rule (RFC 8693) is enforced by the grant handler
            // after validation, which is a token-exchange-specific policy rather than a signature/lifetime check.
            var parameters = new ValidateJwtParameters
            {
                RoleClaimType = JoseClaimNames.Payload.Role,
            }
                .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
                .ValidateIssuer(openIdTenant.Issuer)
                .ValidateTokenLifeTime();

            validateResult = await JsonWebTokenService.ValidateJwtAsync(
                token,
                parameters,
                cancellationToken
            );
        }
        catch (Exception)
        {
            return SubjectTokenValidationResult.Fail(
                errorFactory
                    .InvalidGrant("The presented token is invalid, expired, or revoked.")
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

    private static bool IsJwtTokenType(string tokenTypeIdentifier) =>
        string.Equals(
            tokenTypeIdentifier,
            OpenIdConstants.TokenTypeIdentifiers.AccessToken,
            StringComparison.Ordinal
        )
        || string.Equals(
            tokenTypeIdentifier,
            OpenIdConstants.TokenTypeIdentifiers.IdToken,
            StringComparison.Ordinal
        )
        || string.Equals(
            tokenTypeIdentifier,
            OpenIdConstants.TokenTypeIdentifiers.Jwt,
            StringComparison.Ordinal
        );
}
