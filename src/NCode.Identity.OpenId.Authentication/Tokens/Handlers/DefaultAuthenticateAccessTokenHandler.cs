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
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateAccessTokenCommand"/> message. It
/// validates a self-issued JWT access token against the resolved tenant's signing keys, issuer, lifetime, and the
/// required audience, returning the authenticated principal on success.
/// </summary>
internal class DefaultAuthenticateAccessTokenHandler(IJsonWebTokenService jsonWebTokenService)
    : ICommandResponseHandler<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>
{
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;

    /// <inheritdoc />
    public async ValueTask<AuthenticateAccessTokenDisposition> HandleAsync(
        AuthenticateAccessTokenCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, accessToken, requiredAudiences) = command;

        if (string.IsNullOrEmpty(accessToken))
        {
            return default;
        }

        var openIdTenant = openIdContext.Tenant;

        ValidateJwtResult validateResult;
        try
        {
            // The realm/ownership handlers read roles via ClaimsPrincipal.IsInRole, which keys off the identity's
            // role-claim type; align it with the token's 'role' claim name so a 'GlobalAdmin' role is recognized.
            var parameters = new ValidateJwtParameters
            {
                RoleClaimType = JoseClaimNames.Payload.Role,
            }
                .UseValidationKeys(openIdTenant.SecretsProvider.Collection)
                .ValidateIssuer(openIdTenant.Issuer)
                .ValidateTokenLifeTime();

            if (requiredAudiences.Count > 0)
            {
                parameters.ValidateAudience(requiredAudiences);
            }

            validateResult = await JsonWebTokenService.ValidateJwtAsync(
                accessToken,
                parameters,
                cancellationToken
            );
        }
        catch
        {
            return new AuthenticateAccessTokenDisposition(
                openIdContext.ErrorFactory.InvalidToken()
            );
        }

        if (!validateResult.IsValid)
        {
            return new AuthenticateAccessTokenDisposition(
                openIdContext.ErrorFactory.InvalidToken()
            );
        }

        var claimsIdentity = await validateResult.GetClaimsIdentityAsync(cancellationToken);
        return new AuthenticateAccessTokenDisposition(new ClaimsPrincipal(claimsIdentity));
    }
}
