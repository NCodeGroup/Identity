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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Errors;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="AuthenticateAccessTokenCommand"/> message. It
/// validates a self-issued access token through <see cref="IIssuedTokenValidator"/> — which checks its protection,
/// explicit <c>at+jwt</c> type (RFC 9068 §4), issuer, lifetime, session, and the required audience — then, for a
/// sender-constrained token, verifies the presented DPoP proof (RFC 9449) or client certificate (RFC 8705) matches the
/// token's <c>cnf</c> claim, returning the authenticated principal on success.
/// </summary>
internal class DefaultAuthenticateAccessTokenHandler(
    ILogger<DefaultAuthenticateAccessTokenHandler> logger,
    IIssuedTokenValidator issuedTokenValidator,
    IProofOfPossessionService proofOfPossessionService
) : ICommandResponseHandler<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>
{
    private ILogger<DefaultAuthenticateAccessTokenHandler> Logger { get; } = logger;
    private IIssuedTokenValidator IssuedTokenValidator { get; } = issuedTokenValidator;
    private IProofOfPossessionService ProofOfPossessionService { get; } = proofOfPossessionService;

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

        var options = new AccessTokenValidationOptions { RequiredAudiences = requiredAudiences };
        var validateResult = await IssuedTokenValidator.ValidateAccessTokenAsync(
            openIdContext,
            accessToken,
            options,
            cancellationToken
        );

        if (!validateResult.IsValid)
        {
            Logger.AccessTokenInvalid(validateResult.Exception);
            return new AuthenticateAccessTokenDisposition(
                openIdContext.ErrorFactory.InvalidToken()
            );
        }

        var claimsIdentity = await validateResult.GetClaimsIdentityAsync(cancellationToken);

        // For a sender-constrained token, verify the presented DPoP proof / client certificate matches the token's
        // cnf claim before the principal is trusted (RFC 9449 §7.1, RFC 8705 §3).
        var confirmation = ReadConfirmation(claimsIdentity);
        if (confirmation.HasValue)
        {
            var error = await ProofOfPossessionService.ValidateResourceAccessAsync(
                openIdContext,
                accessToken,
                confirmation,
                cancellationToken
            );
            if (error is not null)
            {
                return new AuthenticateAccessTokenDisposition(error);
            }
        }

        return new AuthenticateAccessTokenDisposition(new ClaimsPrincipal(claimsIdentity));
    }

    private static TokenConfirmation ReadConfirmation(ClaimsIdentity claimsIdentity)
    {
        var value = claimsIdentity.FindFirst(JoseClaimNames.Payload.Cnf)?.Value;
        if (string.IsNullOrEmpty(value))
            return TokenConfirmation.None;

        try
        {
            var root = JsonElement.Parse(value);
            if (root.ValueKind != JsonValueKind.Object)
                return TokenConfirmation.None;

            return new TokenConfirmation
            {
                JwkThumbprint = GetString(root, OpenIdProtocol.ConfirmationMethods.JwkThumbprint),
                X509CertificateSha256Thumbprint = GetString(
                    root,
                    OpenIdProtocol.ConfirmationMethods.X509CertificateSha256Thumbprint
                ),
            };
        }
        catch (JsonException)
        {
            return TokenConfirmation.None;
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetPropertyValue<string>(propertyName, out var value) ? value : null;
}
