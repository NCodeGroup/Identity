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

using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Dispatch;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of <see cref="IBackChannelLogoutNotifier"/> that builds a signed
/// <c>logout_token</c> (OpenID Connect Back-Channel Logout 1.0) and dispatches a retryable
/// <see cref="BackChannelLogoutDeliveryJob"/> that <c>POST</c>s it to the relying party's registered
/// <c>backchannel_logout_uri</c>. The token is protected like the client's ID tokens. A client without a configured
/// endpoint or without a resolvable subject/session is skipped, and one whose registered protection cannot be honored
/// is skipped with a warning; neither fails logout. A reachable-but-failing endpoint is retried out of band.
/// </summary>
internal class DefaultBackChannelLogoutNotifier(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IJwtEncoder jwtEncoder,
    ISubjectIdentifierService subjectIdentifierService,
    IJobDispatcher jobDispatcher,
    ILogger<DefaultBackChannelLogoutNotifier> logger
) : IBackChannelLogoutNotifier
{
    // https://openid.net/specs/openid-connect-backchannel-1_0.html#LogoutToken
    private static readonly TimeSpan LogoutTokenLifetime = TimeSpan.FromMinutes(2);

    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;
    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;
    private ISubjectIdentifierService SubjectIdentifierService { get; } = subjectIdentifierService;
    private IJobDispatcher JobDispatcher { get; } = jobDispatcher;
    private ILogger<DefaultBackChannelLogoutNotifier> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask NotifyAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        string? sessionId,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;

        if (
            !settings.TryGetValue(OpenIdSettingKeys.BackChannelLogoutUri, out var logoutUri)
            || string.IsNullOrEmpty(logoutUri)
        )
        {
            return;
        }

        // A logout_token must identify the subject, the session, or both.
        if (string.IsNullOrEmpty(subjectId) && string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var openIdTenant = openIdContext.Tenant;

        // A logout token is protected like the client's ID tokens: signed with its id_token_signed_response_alg and,
        // when it registers ID token encryption, encrypted to its public key (OpenID Connect Back-Channel Logout 1.0
        // §2.4). A registration the server cannot honor skips the client rather than failing logout or sending a
        // weaker token (ADR-0155).
        ClientJwtCredentials credentials;
        try
        {
            credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
                openIdContext,
                openIdClient,
                ClientJwtProtectionSettings.IdToken,
                cancellationToken
            );
        }
        catch (OpenIdException exception)
        {
            Logger.BackChannelLogoutTokenNotProtected(openIdClient.ClientId, exception);
            return;
        }

        // A logout token is always signed (OpenID Connect Back-Channel Logout 1.0 §2.4).
        if (credentials.SigningCredentials is not { } signingCredentials)
        {
            Logger.BackChannelLogoutTokenNotProtected(openIdClient.ClientId, exception: null);
            return;
        }

        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [JoseClaimNames.Payload.Jti] = CryptoService.GenerateUrlSafeKey(),
            [OpenIdConstants.BackChannelLogout.EventsClaimName] = new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                [OpenIdConstants.BackChannelLogout.EventName] = new Dictionary<string, object>(),
            },
        };

        if (!string.IsNullOrEmpty(subjectId))
        {
            // Project to the client's per-sector pseudonym so a pairwise client's logout_token carries the same `sub`
            // it saw in its id_token (OpenID Connect Core 1.0 §8).
            payloadClaims[JoseClaimNames.Payload.Sub] = SubjectIdentifierService.ProjectSubjectId(
                openIdContext,
                openIdClient,
                subjectId
            );
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            payloadClaims[JoseClaimNames.Payload.Sid] = sessionId;
        }

        var parameters = new JwtEncodingParameters
        {
            TokenType = OpenIdConstants.SecurityTokenTypes.LogoutToken,
            SigningCredentials = signingCredentials,
            EncryptionCredentials = credentials.EncryptionCredentials,
            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,
            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + LogoutTokenLifetime,
            ExtraPayloadClaims = payloadClaims,
            // An encrypted logout token replicates its issuer in the JWE header (OpenID Connect Back-Channel Logout
            // 1.0 §2.4).
            ExtraEncryptionHeaderClaims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JoseClaimNames.Payload.Iss] = openIdTenant.Issuer,
            },
        };

        var logoutToken = JwtEncoder.Encode(parameters);

        // Delivery runs as a durable, retryable dispatch job so a failed POST is retried out of band (and can run on a
        // durable dispatcher adapter); it never fails the logout request.
        await JobDispatcher.DispatchAsync(
            new BackChannelLogoutDeliveryJob
            {
                ClientId = openIdClient.ClientId,
                LogoutUri = logoutUri,
                LogoutToken = logoutToken,
            },
            cancellationToken: cancellationToken
        );
    }
}
