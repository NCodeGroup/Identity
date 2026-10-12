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
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides a default implementation of <see cref="IBackChannelLogoutNotifier"/> that builds a signed
/// <c>logout_token</c> (OpenID Connect Back-Channel Logout 1.0) and sends it as an
/// <c>application/x-www-form-urlencoded</c> <c>POST</c> to the relying party's registered <c>backchannel_logout_uri</c>.
/// Delivery is best-effort: a client without a configured endpoint, or an unreachable endpoint, does not fail logout.
/// </summary>
internal class DefaultBackChannelLogoutNotifier(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ICredentialSelector credentialSelector,
    IJsonWebTokenService jsonWebTokenService,
    IHttpClientFactory httpClientFactory,
    ILogger<DefaultBackChannelLogoutNotifier> logger
) : IBackChannelLogoutNotifier
{
    // https://openid.net/specs/openid-connect-backchannel-1_0.html#LogoutToken
    private static readonly TimeSpan LogoutTokenLifetime = TimeSpan.FromMinutes(2);

    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private ICredentialSelector CredentialSelector { get; } = credentialSelector;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
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

        if (
            !CredentialSelector.TryGetSigningCredentials(
                AlgorithmCollectionProvider.Collection,
                settings.GetValue(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported),
                openIdTenant.SecretsProvider.Collection,
                out var signingCredentials
            )
        )
        {
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
            payloadClaims[JoseClaimNames.Payload.Sub] = subjectId;
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            payloadClaims[JoseClaimNames.Payload.Sid] = sessionId;
        }

        var parameters = new EncodeJwtParameters
        {
            TokenType = OpenIdConstants.SecurityTokenTypes.LogoutToken,
            SigningCredentials = signingCredentials,
            Issuer = openIdTenant.Issuer,
            Audience = openIdClient.ClientId,
            IssuedAt = createdWhen,
            NotBefore = createdWhen,
            Expires = createdWhen + LogoutTokenLifetime,
            ExtraPayloadClaims = payloadClaims,
        };

        var logoutToken = JsonWebTokenService.EncodeJwt(parameters);

        KeyValuePair<string, string?>[] formValues =
        [
            new(OpenIdConstants.Parameters.LogoutToken, logoutToken),
        ];
        using var content = new FormUrlEncodedContent(formValues);

        try
        {
            var httpClient = HttpClientFactory.CreateClient();
            using var response = await httpClient.PostAsync(logoutUri, content, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Best-effort: a relying party whose back-channel endpoint is unreachable does not fail the logout.
            Logger.BackChannelLogoutDeliveryFailed(openIdClient.ClientId, exception);
        }
    }
}
