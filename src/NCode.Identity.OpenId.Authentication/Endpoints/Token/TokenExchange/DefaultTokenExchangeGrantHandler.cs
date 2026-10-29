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

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Settings;
using NCode.Json;
using NCode.Persistence.Stores;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;

/// <summary>
/// Provides the default implementation of <see cref="ITokenGrantHandler"/> for the OAuth 2.0 Token Exchange grant
/// (<see href="https://datatracker.ietf.org/doc/html/rfc8693">RFC 8693</see>). It validates the presented
/// <c>subject_token</c> (and optional <c>actor_token</c>), enforces that the requesting client is entitled to the
/// presented tokens, authorizes delegation via the subject token's <c>may_act</c> claim, resolves the subject to a
/// server-owned principal, and mints the requested token — emitting an <c>act</c> claim for a delegated exchange.
/// </summary>
internal class DefaultTokenExchangeGrantHandler(
    TimeProvider timeProvider,
    ITokenService tokenService,
    ISubjectTokenValidator subjectTokenValidator,
    IPrincipalResolver principalResolver,
    IStoreManagerFactory storeManagerFactory,
    IClientScopeService clientScopeService
) : ITokenGrantHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ITokenService TokenService { get; } = tokenService;
    private ISubjectTokenValidator SubjectTokenValidator { get; } = subjectTokenValidator;
    private IPrincipalResolver PrincipalResolver { get; } = principalResolver;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IClientScopeService ClientScopeService { get; } = clientScopeService;

    /// <inheritdoc />
    public IReadOnlySet<string> GrantTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { OpenIdProtocol.GrantTypes.TokenExchange };

    /// <inheritdoc />
    public async ValueTask<IOpenIdResponse> HandleAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var mediator = openIdContext.Mediator;

        // Token exchange is a powerful grant; restrict it to confidential clients so a leaked public-client identifier
        // cannot mint tokens for arbitrary subjects (RFC 8693 §2.1; aligns with Keycloak's standard token exchange).
        if (!openIdClient.IsConfidential)
        {
            return errorFactory
                .UnauthorizedClient("Token exchange requires a confidential client.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var subjectToken = tokenRequest.SubjectToken;
        if (string.IsNullOrEmpty(subjectToken))
        {
            return errorFactory
                .MissingParameter(OpenIdProtocol.Parameters.SubjectToken)
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var subjectTokenType = tokenRequest.SubjectTokenType;
        if (string.IsNullOrEmpty(subjectTokenType))
        {
            return errorFactory
                .MissingParameter(OpenIdProtocol.Parameters.SubjectTokenType)
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var subjectResult = await SubjectTokenValidator.ValidateAsync(
            openIdContext,
            openIdClient,
            subjectToken,
            subjectTokenType,
            cancellationToken
        );
        if (!subjectResult.IsValid)
        {
            return subjectResult.Error;
        }

        if (!IsRequesterEntitled(openIdClient, subjectResult.Payload))
        {
            return errorFactory
                .InvalidGrant("The subject_token was not issued to the requesting client.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        // Validate the requested output type and refresh-token policy before any expensive work (principal resolution,
        // token minting), so a malformed or disallowed request fails fast.
        var requestedTokenType =
            tokenRequest.RequestedTokenType ?? OpenIdProtocol.TokenTypeIdentifiers.AccessToken;
        if (!IsSupportedRequestedTokenType(requestedTokenType))
        {
            return errorFactory
                .InvalidRequest(
                    $"The requested_token_type '{requestedTokenType}' is not supported by this authorization server."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        if (
            string.Equals(
                requestedTokenType,
                OpenIdProtocol.TokenTypeIdentifiers.RefreshToken,
                StringComparison.Ordinal
            ) && !openIdClient.Settings.GetValue(OpenIdSettingKeys.TokenExchangeAllowRefreshToken)
        )
        {
            return errorFactory
                .InvalidRequest(
                    "Issuing a refresh token via token exchange is not enabled for this client."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        // A refresh token issued by exchange joins the horizon of the authentication it carries, so exchange cannot
        // renew a family. See ADR-0297.
        DateTimeOffset? lineageCreatedWhen = null;
        if (
            string.Equals(
                requestedTokenType,
                OpenIdProtocol.TokenTypeIdentifiers.RefreshToken,
                StringComparison.Ordinal
            )
        )
        {
            var authTime = ReadAuthTime(subjectResult.Payload);
            if (authTime is null)
            {
                return errorFactory
                    .InvalidGrant(
                        "The subject_token carries no auth_time, so a refresh token issued for it could outlive the authentication."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }

            if (
                RefreshTokenExpiration.HasLineageExpired(
                    openIdClient.Settings,
                    authTime.Value,
                    TimeProvider.GetUtcNowWithPrecisionInSeconds()
                )
            )
            {
                return errorFactory
                    .InvalidGrant(
                        "The subject_token's authentication is older than the refresh token lifetime."
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }

            lineageCreatedWhen = authTime;
        }

        // actor_token present => delegation; otherwise impersonation.
        SubjectAuthentication? actorAuthentication = null;
        IReadOnlyDictionary<string, object>? actClaim = null;
        var actorToken = tokenRequest.ActorToken;
        if (!string.IsNullOrEmpty(actorToken))
        {
            var delegationError = await TryBuildDelegationAsync(
                openIdContext,
                openIdClient,
                tokenRequest,
                actorToken,
                subjectResult.Payload,
                cancellationToken
            );
            if (delegationError.Error is not null)
            {
                return delegationError.Error;
            }

            actorAuthentication = delegationError.ActorAuthentication;
            actClaim = delegationError.ActClaim;
        }

        var principalId = await ResolvePrincipalIdOrDefaultAsync(
            openIdContext,
            subjectResult.Principal,
            cancellationToken
        );
        if (string.IsNullOrEmpty(principalId))
        {
            return errorFactory
                .InvalidGrant(
                    "The subject_token's subject could not be resolved to a known principal."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var authenticationProperties = new AuthenticationProperties
        {
            IssuedUtc = ReadAuthTime(subjectResult.Payload) ?? createdWhen,
        };

        // The subject token was validated as one this tenant issued, so the subject belongs to this tenant; an ID token
        // carries no tid claim. See ADR-0225.
        var tenantId = openIdContext.Tenant.TenantId;
        authenticationProperties.SetString(
            OpenIdConstants.AuthenticationPropertyItems.TenantId,
            tenantId
        );

        var subjectAuthentication = SubjectAuthentication.Create(
            openIdContext,
            OpenIdProtocol.GrantTypes.TokenExchange,
            authenticationProperties,
            subjectResult.Principal,
            principalId
        );

        var grant = new TokenExchangeGrant
        {
            SubjectAuthentication = subjectAuthentication,
            ActorAuthentication = actorAuthentication,
            RequestedTokenType = requestedTokenType,
        };

        await mediator.SendAsync(
            new ValidateTokenGrantCommand<TokenExchangeGrant>(
                openIdContext,
                openIdClient,
                tokenRequest,
                grant
            ),
            cancellationToken
        );

        var effectiveScopes = await ResolveEffectiveScopesAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            subjectResult.Payload,
            cancellationToken
        );

        return await CreateTokenResponseAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            grant,
            effectiveScopes,
            actClaim,
            createdWhen,
            lineageCreatedWhen,
            proofOfPossessionBinding,
            cancellationToken
        );
    }

    private readonly record struct DelegationResult(
        IOpenIdError? Error,
        SubjectAuthentication? ActorAuthentication,
        IReadOnlyDictionary<string, object>? ActClaim
    );

    private async ValueTask<DelegationResult> TryBuildDelegationAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        string actorToken,
        JsonElement subjectPayload,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        var actorTokenType = tokenRequest.ActorTokenType;
        if (string.IsNullOrEmpty(actorTokenType))
        {
            return new DelegationResult(
                errorFactory
                    .MissingParameter(OpenIdProtocol.Parameters.ActorTokenType)
                    .WithStatusCode(StatusCodes.Status400BadRequest),
                null,
                null
            );
        }

        var actorResult = await SubjectTokenValidator.ValidateAsync(
            openIdContext,
            openIdClient,
            actorToken,
            actorTokenType,
            cancellationToken
        );
        if (!actorResult.IsValid)
        {
            return new DelegationResult(actorResult.Error, null, null);
        }

        if (!IsRequesterEntitled(openIdClient, actorResult.Payload))
        {
            return new DelegationResult(
                errorFactory
                    .InvalidGrant("The actor_token was not issued to the requesting client.")
                    .WithStatusCode(StatusCodes.Status400BadRequest),
                null,
                null
            );
        }

        var subjectSub = GetStringClaim(subjectPayload, JoseClaimNames.Payload.Sub);
        var actorSub = GetStringClaim(actorResult.Payload, JoseClaimNames.Payload.Sub);
        if (string.IsNullOrEmpty(actorSub))
        {
            return new DelegationResult(
                errorFactory
                    .InvalidGrant("The actor_token does not contain a subject.")
                    .WithStatusCode(StatusCodes.Status400BadRequest),
                null,
                null
            );
        }

        // The subject and actor must differ; a token that delegates to itself is meaningless and masks a misconfigured
        // actor_token (aligns with Keycloak's delegation validation, RFC 8693 §4.1).
        if (string.Equals(subjectSub, actorSub, StringComparison.Ordinal))
        {
            return new DelegationResult(
                errorFactory
                    .InvalidGrant("The actor_token and subject_token identify the same subject.")
                    .WithStatusCode(StatusCodes.Status400BadRequest),
                null,
                null
            );
        }

        // The subject must have pre-authorized this actor via a may_act claim (RFC 8693 §4.4): the server never lets an
        // arbitrary actor act for a subject without the subject token explicitly naming it.
        var mayActError = ValidateMayAct(
            openIdContext,
            subjectPayload,
            actorResult.Payload,
            actorSub
        );
        if (mayActError is not null)
        {
            return new DelegationResult(mayActError, null, null);
        }

        var actClaim = BuildActClaim(actorResult.Payload, actorSub, subjectPayload);

        // The actor principal is identified in the act claim; it does not need full principal resolution because the
        // issued token's subject remains the delegating party, not the actor.
        return new DelegationResult(null, null, actClaim);
    }

    private static IOpenIdError? ValidateMayAct(
        OpenIdContext openIdContext,
        JsonElement subjectPayload,
        JsonElement actorPayload,
        string actorSub
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        if (
            !subjectPayload.TryGetProperty(JoseClaimNames.Payload.MayAct, out var mayAct)
            || mayAct.ValueKind != JsonValueKind.Object
        )
        {
            return errorFactory
                .InvalidGrant("The subject_token does not authorize the presented actor.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        var mayActSub = GetStringClaim(mayAct, JoseClaimNames.Payload.Sub);
        if (!string.Equals(mayActSub, actorSub, StringComparison.Ordinal))
        {
            return errorFactory
                .InvalidGrant("The subject_token does not authorize the presented actor.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        // When may_act pins a client_id, the actor_token must have been issued to that client.
        var mayActClientId = GetStringClaim(mayAct, JoseClaimNames.Payload.ClientId);
        if (!string.IsNullOrEmpty(mayActClientId))
        {
            var actorClientId = GetStringClaim(actorPayload, JoseClaimNames.Payload.ClientId);
            if (!string.Equals(mayActClientId, actorClientId, StringComparison.Ordinal))
            {
                return errorFactory
                    .InvalidGrant("The subject_token does not authorize the presented actor.")
                    .WithStatusCode(StatusCodes.Status400BadRequest);
            }
        }

        // When may_act pins an issuer, it must match this tenant (self-issued tokens only in this implementation).
        var mayActIssuer = GetStringClaim(mayAct, JoseClaimNames.Payload.Iss);
        if (
            !string.IsNullOrEmpty(mayActIssuer)
            && !string.Equals(mayActIssuer, openIdContext.Tenant.Issuer, StringComparison.Ordinal)
        )
        {
            return errorFactory
                .InvalidGrant("The subject_token does not authorize the presented actor.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
        }

        return null;
    }

    private static Dictionary<string, object> BuildActClaim(
        JsonElement actorPayload,
        string actorSub,
        JsonElement subjectPayload
    )
    {
        var act = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JoseClaimNames.Payload.Sub] = actorSub,
        };

        var actorClientId = GetStringClaim(actorPayload, JoseClaimNames.Payload.ClientId);
        if (!string.IsNullOrEmpty(actorClientId))
        {
            act[JoseClaimNames.Payload.ClientId] = actorClientId;
        }

        // Preserve the delegation chain: if the subject token already carries an act claim, nest it inside the new
        // actor's act claim so the full chain of prior actors remains expressible (RFC 8693 §4.1).
        if (
            subjectPayload.TryGetProperty(JoseClaimNames.Payload.Act, out var existingAct)
            && existingAct.ValueKind == JsonValueKind.Object
        )
        {
            act[JoseClaimNames.Payload.Act] = existingAct.Clone();
        }

        return act;
    }

    private async ValueTask<string?> ResolvePrincipalIdOrDefaultAsync(
        OpenIdContext openIdContext,
        System.Security.Claims.ClaimsPrincipal subject,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        return await PrincipalResolver.ResolvePrincipalIdOrDefaultAsync(
            openIdContext,
            subject,
            storeManager,
            cancellationToken
        );
    }

    private async ValueTask<IReadOnlyList<string>> ResolveEffectiveScopesAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        JsonElement subjectPayload,
        CancellationToken cancellationToken
    )
    {
        // When the client requests scopes they have already been validated against the client's allowed scopes by
        // DefaultValidateTokenRequestHandler, so they are safe to honor as-is.
        if (tokenRequest.Scopes is { Count: > 0 } requestedScopes)
        {
            return requestedScopes;
        }

        // Otherwise default to the subject token's scopes, intersected with what this client is allowed, so an exchange
        // can never widen the requester's authority beyond its own grants (secure downscoping default).
        var subjectScopes = GetScopeClaim(subjectPayload);
        if (subjectScopes.Length == 0)
        {
            return [];
        }

        var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
            openIdContext.Tenant.TenantId,
            openIdClient.ClientId,
            cancellationToken
        );
        var allowed = new HashSet<string>(allowedScopes, StringComparer.Ordinal);
        return subjectScopes.Where(allowed.Contains).ToList();
    }

    private async ValueTask<IOpenIdResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        ITokenRequest tokenRequest,
        TokenExchangeGrant grant,
        IReadOnlyList<string> effectiveScopes,
        IReadOnlyDictionary<string, object>? actClaim,
        DateTimeOffset createdWhen,
        DateTimeOffset? lineageCreatedWhen,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var tokenResponse = TokenResponse.Create(openIdEnvironment);
        tokenResponse.Scopes = effectiveScopes.ToList();

        // RFC 8707 resource indicators and the RFC 8693 audience parameter both narrow the issued token's audience to
        // specific resource servers; combine them into the resource set the credential resolver honors.
        var resources = CombineAudienceAndResources(tokenRequest);

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = createdWhen,
            RefreshTokenLineageCreatedWhen = lineageCreatedWhen,
            GrantType = OpenIdProtocol.GrantTypes.TokenExchange,
            OriginalScopes = effectiveScopes,
            EffectiveScopes = effectiveScopes,
            Resources = resources,
            SubjectAuthentication = grant.SubjectAuthentication,
            Confirmation = proofOfPossessionBinding.Confirmation,
            ExtraPayloadClaims = actClaim is null
                ? null
                : new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [JoseClaimNames.Payload.Act] = actClaim,
                },
        };

        if (
            string.Equals(
                grant.RequestedTokenType,
                OpenIdProtocol.TokenTypeIdentifiers.IdToken,
                StringComparison.Ordinal
            )
        )
        {
            var idToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken
            );

            // RFC 8693 §2.2.1: the issued token is always returned in the access_token field even when it is not an
            // access token; token_type is not applicable for an id_token.
            tokenResponse.AccessToken = idToken.TokenValue;
            tokenResponse.ExpiresIn = idToken.TokenLifetime.Duration;
            tokenResponse.TokenType = OpenIdProtocol.TokenTypes.NotApplicable;
            tokenResponse.IssuedTokenType = OpenIdProtocol.TokenTypeIdentifiers.IdToken;
            return tokenResponse;
        }

        var accessToken = await TokenService.CreateAccessTokenAsync(
            openIdContext,
            openIdClient,
            securityTokenRequest,
            cancellationToken
        );

        tokenResponse.AccessToken = accessToken.TokenValue;
        tokenResponse.ExpiresIn = accessToken.TokenLifetime.Duration;
        tokenResponse.TokenType = proofOfPossessionBinding.TokenType;
        tokenResponse.IssuedTokenType = OpenIdProtocol.TokenTypeIdentifiers.AccessToken;

        if (
            string.Equals(
                grant.RequestedTokenType,
                OpenIdProtocol.TokenTypeIdentifiers.RefreshToken,
                StringComparison.Ordinal
            )
        )
        {
            var refreshTokenRequest = securityTokenRequest with
            {
                AccessToken = tokenResponse.AccessToken,
            };

            var refreshToken = await TokenService.CreateRefreshTokenAsync(
                openIdContext,
                openIdClient,
                refreshTokenRequest,
                cancellationToken
            );

            tokenResponse.SetRefreshToken(refreshToken);
            tokenResponse.IssuedTokenType = OpenIdProtocol.TokenTypeIdentifiers.RefreshToken;
        }

        return tokenResponse;
    }

    private static List<string>? CombineAudienceAndResources(ITokenRequest tokenRequest)
    {
        var combined = new List<string>();

        if (tokenRequest.Resources is { Count: > 0 } resources)
        {
            combined.AddRange(resources);
        }

        if (tokenRequest.Audience is { Count: > 0 } audience)
        {
            combined.AddRange(audience);
        }

        return combined.Count == 0 ? null : combined;
    }

    private static bool IsRequesterEntitled(OpenIdClient openIdClient, JsonElement payload)
    {
        // The requester must be the client the presented token was issued to, or be named in the token's audience, so a
        // client cannot exchange a token that was never intended for it (RFC 8693; aligns with Keycloak).
        var tokenClientId = GetStringClaim(payload, JoseClaimNames.Payload.ClientId);
        if (string.Equals(tokenClientId, openIdClient.ClientId, StringComparison.Ordinal))
        {
            return true;
        }

        return GetAudiences(payload).Contains(openIdClient.ClientId, StringComparer.Ordinal);
    }

    private static bool IsSupportedRequestedTokenType(string requestedTokenType) =>
        string.Equals(
            requestedTokenType,
            OpenIdProtocol.TokenTypeIdentifiers.AccessToken,
            StringComparison.Ordinal
        )
        || string.Equals(
            requestedTokenType,
            OpenIdProtocol.TokenTypeIdentifiers.RefreshToken,
            StringComparison.Ordinal
        )
        || string.Equals(
            requestedTokenType,
            OpenIdProtocol.TokenTypeIdentifiers.IdToken,
            StringComparison.Ordinal
        );

    private static DateTimeOffset? ReadAuthTime(JsonElement payload) =>
        payload.TryGetPropertyNumericDate(JoseClaimNames.Payload.AuthTime, out var authTime)
            ? authTime
            : null;

    private static string? GetStringClaim(JsonElement element, string name) =>
        element.TryGetPropertyValue<string>(name, out var value) ? value : null;

    private static List<string> GetAudiences(JsonElement payload)
    {
        var audiences = new List<string>();
        if (!payload.TryGetProperty(JoseClaimNames.Payload.Aud, out var aud))
        {
            return audiences;
        }

        if (aud.TryGetValue<string>(out var single))
        {
            audiences.Add(single);
        }
        else if (aud.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in aud.EnumerateArray())
            {
                if (element.TryGetValue<string>(out var value))
                {
                    audiences.Add(value);
                }
            }
        }

        return audiences;
    }

    private static string[] GetScopeClaim(JsonElement payload)
    {
        var scope = GetStringClaim(payload, JoseClaimNames.Payload.Scope);
        return string.IsNullOrEmpty(scope)
            ? []
            : scope.Split(
                OpenIdProtocol.ParameterSeparatorChar,
                StringSplitOptions.RemoveEmptyEntries
            );
    }
}
