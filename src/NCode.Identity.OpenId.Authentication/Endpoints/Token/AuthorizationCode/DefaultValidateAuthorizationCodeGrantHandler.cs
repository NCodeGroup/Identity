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

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="AuthorizationGrant"/>.
/// </summary>
internal class DefaultValidateAuthorizationCodeGrantHandler(
    IClientScopeService clientScopeService,
    ICodeChallengeMethodCollectionProvider codeChallengeMethodCollectionProvider
) : ICommandHandler<ValidateTokenGrantCommand<AuthorizationGrant>>, ISupportMediatorPriority
{
    private IClientScopeService ClientScopeService { get; } = clientScopeService;
    private ICodeChallengeMethodCollectionProvider CodeChallengeMethodCollectionProvider { get; } =
        codeChallengeMethodCollectionProvider;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<AuthorizationGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest, authorizationGrant) = command;
        var (authorizationRequest, subjectAuthentication) = authorizationGrant;

        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // see DefaultValidateTokenRequestHandler for additional validation such as scope, etc

        // client_id from authorization request
        if (
            !string.Equals(
                openIdClient.ClientId,
                authorizationRequest.ClientId,
                StringComparison.Ordinal
            )
        )
            throw errorFactory
                .InvalidGrant("The provided authorization code was issued to a different client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // redirect_uri from token request
        var redirectUri = tokenRequest.RedirectUri;
        if (redirectUri is null)
            // invalid_request
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // redirect_uri from authorization request
        if (authorizationRequest.RedirectUri != redirectUri)
            // invalid_grant
            throw errorFactory
                .InvalidGrant(
                    "The provided redirect uri does not match from the authorization request."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // scope
        var requestedScopes = tokenRequest.Scopes;
        var originalScopes = authorizationRequest.Scopes;
        var effectiveScopes = requestedScopes ?? originalScopes;

        // extra scopes
        var hasExtraScopes = requestedScopes?.Except(originalScopes).Any() ?? false;
        if (hasExtraScopes)
            // invalid_scope
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException(
                    "The requested scope exceeds the scope granted by the resource owner."
                );

        // A requested scope must be permitted by the client's grants and must be an actual scope value currently
        // defined on one of the tenant's enabled resource servers (ADR-0026); GetAllowedScopesAsync already
        // intersects the client's grants with the live resource-server scope definitions, so this one check both
        // enforces the grant ceiling and validates the actual scope values.
        var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
            openIdContext.Tenant.TenantId,
            openIdClient.ClientId,
            cancellationToken
        );
        if (effectiveScopes.Except(allowedScopes).Any())
            // invalid_scope
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // validate the subject
        await ValidateSubjectAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            subjectAuthentication,
            cancellationToken
        );

        // RFC 8707: a resource requested at the token endpoint must be within the resources authorized at the
        // authorization endpoint. The token service additionally rejects (invalid_target) any requested resource that
        // does not resolve to an enabled resource server owning one of the effective scopes.
        var requestedResources = tokenRequest.Resources;
        var authorizedResources = authorizationRequest.Resources;
        if (
            requestedResources is { Count: > 0 }
            && authorizedResources.Count > 0
            && requestedResources.Except(authorizedResources).Any()
        )
            throw errorFactory
                .InvalidTarget(
                    "The requested resource was not authorized in the original authorization request."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // DPoP binding continuity (dpop_jkt, RFC 9449 §10) is enforced in DefaultAuthorizationCodeGrantHandler, where
        // the presented proof-of-possession binding is available.

        // PKCE: code_verifier, code_challenge, code_challenge_method
        ValidatePkce(
            errorFactory,
            tokenRequest.CodeVerifier,
            authorizationRequest.CodeChallenge,
            authorizationRequest.CodeChallengeMethod,
            settings.GetValue(OpenIdSettingKeys.RequireCodeChallenge)
        );
    }

    private static async ValueTask ValidateSubjectAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        await mediator.SendAsync(
            new ValidateSubjectAuthenticationCommand(
                openIdContext,
                openIdClient,
                openIdRequest,
                subjectAuthentication,
                operationDisposition,
                // Back-channel code exchange: authentication-age freshness was already enforced at authorization, so
                // do not re-check the fixed auth_time against the later token-endpoint clock.
                EnforceAuthenticationAge: false
            ),
            cancellationToken
        );

        if (operationDisposition.HasError)
        {
            throw operationDisposition
                .Error.WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }
    }

    private void ValidatePkce(
        IOpenIdErrorFactory errorFactory,
        string? codeVerifier,
        string? codeChallenge,
        string? codeChallengeMethod,
        bool requireCodeChallenge
    )
    {
        var hasCodeChallenge = !string.IsNullOrEmpty(codeChallenge);
        var requireCodeVerifier = hasCodeChallenge || requireCodeChallenge;

        if (!string.IsNullOrEmpty(codeVerifier))
        {
            if (!hasCodeChallenge)
                throw errorFactory
                    .InvalidGrant(
                        "The 'code_challenge' parameter was missing in the original authorization request."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();

            if (
                string.IsNullOrEmpty(codeChallengeMethod)
                || !CodeChallengeMethodCollectionProvider.TryGet(
                    codeChallengeMethod,
                    out var codeChallengeMethodHandler
                )
            )
                throw errorFactory
                    .InvalidGrant(
                        "The provided 'code_challenge_method' parameter contains a value that is not supported."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();

            var expectedCodeChallenge = codeChallengeMethodHandler.ComputeChallenge(codeVerifier);

            if (
                !CryptographicOperations.FixedTimeEquals(
                    MemoryMarshal.AsBytes(expectedCodeChallenge.AsSpan()),
                    MemoryMarshal.AsBytes(codeChallenge.AsSpan())
                )
            )
                throw errorFactory
                    .InvalidGrant("PKCE verification failed.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }
        else if (requireCodeVerifier)
        {
            throw errorFactory
                .InvalidGrant("The 'code_verifier' parameter is required.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }
    }
}
