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

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="ILogoutTokenValidator"/> that verifies the signature of a logout
/// token with the provider's signing keys, checks its claims (OpenID Connect Back-Channel Logout 1.0 §2.6) and records
/// its <c>jti</c> so that the same token validates only once (ADR-0274).
/// </summary>
internal class DefaultLogoutTokenValidator(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    IProviderMetadataResolver metadataResolver,
    IProviderKeySetResolver keySetResolver,
    IJwtValidator jwtValidator,
    IExpiringKeySet expiringKeySet
) : ILogoutTokenValidator
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(10);

    private const string EventsClaimName = "events";
    private const string BackChannelLogoutEvent =
        "http://schemas.openid.net/event/backchannel-logout";
    private const string ReplayKeyPurpose = "logout_token_jti";

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private IProviderKeySetResolver KeySetResolver { get; } = keySetResolver;
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;

    /// <inheritdoc />
    public async ValueTask<LogoutTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        LogoutTokenValidationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.LogoutToken))
            return Fail("The logout token is empty.");

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var issuer =
            metadata.Issuer
            ?? throw new OpenIdProviderException(
                "The discovery document of the provider lacks the issuer."
            );

        var algorithms = ProviderSignaturePolicy.GetPermittedAlgorithms(
            AlgorithmCollectionProvider.Collection,
            registration,
            metadata
        );
        if (algorithms.Count == 0)
            return Fail(
                "No logout-token signing algorithm is both permitted by the client and advertised by the provider."
            );

        var parameters = new JwtValidationParameters
        {
            ClockSkew = ClockSkew,
            ValidSignatureAlgorithms = algorithms,
            ResolveValidationKeysAsync = (token, _, _, ct) =>
                ProviderSignaturePolicy.ResolveKeysAsync(
                    KeySetResolver,
                    registration.Authority,
                    token,
                    ct
                ),
        }
            .ValidateIssuer(issuer)
            .ValidateAudience(registration.ClientId)
            .ValidateTokenLifeTime(o =>
            {
                o.RequireIssuedAt = true;
                o.MaxLifetime = MaxLifetime;
            });

        var result = await JwtValidator.ValidateAsync(
            request.LogoutToken,
            parameters,
            cancellationToken
        );

        if (!result.IsValid)
        {
            var exception = result.Exception;
            ProviderSignaturePolicy.RethrowProviderFailure(exception);

            return Fail(
                exception is JwtDecodeException or JwtSecretKeyNotFoundException
                    ? "The logout token signature could not be verified with the provider's signing keys."
                    : "The logout token is invalid: " + exception.Message
            );
        }

        var header = result.DecodedJwt.Header;
        var payload = result.DecodedJwt.Payload;

        if (
            header.TryGetProperty(JoseClaimNames.Header.Typ, out var typ)
            && (
                typ.ValueKind != JsonValueKind.String
                || !JoseTokenTypes.AreSame(typ.GetString(), JoseTokenTypes.LogoutJwt)
            )
        )
            return Fail("The 'typ' header of the logout token is not logout+jwt.");

        string? sub = null;
        if (payload.TryGetProperty(JoseClaimNames.Payload.Sub, out var subElement))
        {
            if (
                subElement.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(sub = subElement.GetString())
            )
                return Fail("The 'sub' claim of the logout token is not a non-empty string.");
        }

        string? sid = null;
        if (payload.TryGetProperty(JoseClaimNames.Payload.Sid, out var sidElement))
        {
            if (
                sidElement.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(sid = sidElement.GetString())
            )
                return Fail("The 'sid' claim of the logout token is not a non-empty string.");
        }

        if (sub is null && sid is null)
            return Fail("The logout token has neither a 'sub' nor a 'sid' claim.");

        if (registration.BackchannelLogoutSessionRequired && sid is null)
            return Fail("The logout token has no 'sid' claim, which the client requires.");

        if (
            !payload.TryGetProperty(EventsClaimName, out var events)
            || events.ValueKind != JsonValueKind.Object
            || !events.TryGetProperty(BackChannelLogoutEvent, out var logoutEvent)
            || logoutEvent.ValueKind != JsonValueKind.Object
        )
            return Fail(
                "The 'events' claim of the logout token does not contain the back-channel logout event."
            );

        if (payload.TryGetProperty(JoseClaimNames.Payload.Nonce, out _))
            return Fail("The logout token contains a 'nonce' claim.");

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Jti, out var jtiElement)
            || jtiElement.ValueKind != JsonValueKind.String
            || jtiElement.GetString() is not { Length: > 0 } jti
        )
            return Fail("The logout token has no 'jti' claim.");

        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Exp, out var expElement)
            || expElement.ValueKind != JsonValueKind.Number
            || !expElement.TryGetDouble(out var expSeconds)
        )
            return Fail("The logout token has no numeric 'exp' claim.");

        var expiresAt = DateTimeOffset.UnixEpoch.AddSeconds(expSeconds);

        if (
            !await ExpiringKeySet.TryAddAsync(
                CreateReplayKey(issuer, registration.ClientId, jti),
                expiresAt + ClockSkew,
                cancellationToken
            )
        )
            return Fail("The logout token has already been received.");

        return new LogoutTokenValidationResult(payload, jti, expiresAt, sub, sid);
    }

    private static string CreateReplayKey(string issuer, string clientId, string jti)
    {
        var material = string.Create(
            CultureInfo.InvariantCulture,
            $"{issuer.Length}:{issuer}{clientId.Length}:{clientId}{jti}"
        );

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(SecureEncoding.UTF8.GetBytes(material), digest);

        return ReplayKeyPurpose + ":" + Convert.ToHexStringLower(digest);
    }

    private static LogoutTokenValidationResult Fail(string reason) => new(reason);
}
