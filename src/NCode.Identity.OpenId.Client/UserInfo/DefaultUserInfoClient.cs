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

using System.Net.Http.Headers;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.ProofOfPossession;
using NCode.Identity.OpenId.Client.Tokens;

namespace NCode.Identity.OpenId.Client.UserInfo;

/// <summary>
/// Provides the default implementation of <see cref="IUserInfoClient"/>. It sends a <c>GET</c> to the UserInfo
/// endpoint, or to its mTLS alias for a client that authenticates with mutual TLS, presenting the access token as a
/// bearer token or with a DPoP proof that it retries once with the nonce the endpoint supplies (RFC 9449 §9), and maps
/// a JSON response to claims the way the JWT stack maps a payload (ADR-0122).
/// </summary>
// See ADR-0299.
internal class DefaultUserInfoClient(
    IProviderMetadataResolver metadataResolver,
    ClientRequestSender sender,
    ProviderHttpClient providerHttpClient,
    DPoPKeyResolver dpopKeyResolver,
    IClaimsIdentityFactory claimsIdentityFactory
) : IUserInfoClient
{
    /// <summary>
    /// The authentication type of the identity a UserInfo response maps to.
    /// </summary>
    internal const string AuthenticationType = "UserInfo";

    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;
    private ClientRequestSender Sender { get; } = sender;
    private ProviderHttpClient ProviderHttpClient { get; } = providerHttpClient;
    private DPoPKeyResolver DPoPKeyResolver { get; } = dpopKeyResolver;
    private IClaimsIdentityFactory ClaimsIdentityFactory { get; } = claimsIdentityFactory;

    /// <inheritdoc />
    public async ValueTask<UserInfoResult> GetUserInfoAsync(
        ClientRegistration registration,
        UserInfoRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var userInfoEndpoint = metadata.UserInfoEndpoint;
        if (string.IsNullOrEmpty(userInfoEndpoint))
            throw new InvalidOperationException(
                $"The OpenID Provider '{registration.Authority}' does not advertise a UserInfo endpoint."
            );

        var target = Sender.ResolveTarget(
            registration,
            metadata,
            new ProviderEndpoint(OpenIdProtocol.MetadataNames.UserInfoEndpoint, userInfoEndpoint)
        );

        DPoPProofKey? dpopKey = null;
        if (!string.IsNullOrEmpty(request.DPoPJwkThumbprint))
            dpopKey =
                DPoPKeyResolver.Resolve(registration, metadata, request.DPoPJwkThumbprint)
                ?? throw new InvalidOperationException(
                    $"The client '{registration.ClientId}' cannot present its DPoP-bound access token: it has no DPoP key with the thumbprint the token is bound to."
                );

        var response = await SendOnceAsync(target, request.AccessToken, dpopKey, cancellationToken);
        if (dpopKey is not null && !string.IsNullOrEmpty(response.DPoPNonce))
        {
            dpopKey.RecordNonce(target.Uri, response.DPoPNonce);

            // RFC 9449 §9: a resource that requires a nonce answers use_dpop_nonce with one; retry exactly once.
            if (IsUseDPoPNonce(response))
            {
                response = await SendOnceAsync(
                    target,
                    request.AccessToken,
                    dpopKey,
                    cancellationToken
                );

                if (!string.IsNullOrEmpty(response.DPoPNonce))
                    dpopKey.RecordNonce(target.Uri, response.DPoPNonce);
            }
        }

        return ReadResult(response, request, metadata);
    }

    private static bool IsUseDPoPNonce(ProviderHttpResponse response) =>
        ProtocolErrorReader.TryReadChallenge(response, out var error)
        && string.Equals(
            error.Error,
            OpenIdProtocol.ErrorCodes.UseDPoPNonce,
            StringComparison.Ordinal
        );

    private async ValueTask<ProviderHttpResponse> SendOnceAsync(
        ProviderTarget target,
        string accessToken,
        DPoPProofKey? dpopKey,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, target.Uri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(OpenIdProtocol.MediaTypes.Json)
        );

        if (dpopKey is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                OpenIdProtocol.TokenTypes.Bearer,
                accessToken
            );
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                OpenIdProtocol.TokenTypes.DPoP,
                accessToken
            );
            request.Headers.TryAddWithoutValidation(
                OpenIdProtocol.DPoP.HeaderName,
                dpopKey.CreateProof(HttpMethod.Get, target.Uri, accessToken)
            );
        }

        return await ProviderHttpClient.SendAsync(
            request,
            target.HttpClientName,
            cancellationToken
        );
    }

    private UserInfoResult ReadResult(
        ProviderHttpResponse response,
        UserInfoRequest request,
        OpenIdProviderMetadata metadata
    )
    {
        if (response.StatusCode == ProviderStatusCodes.Status200OK)
            return ReadClaims(response, request, metadata);

        if (
            ProtocolErrorReader.TryReadChallenge(response, out var error)
            || ProtocolErrorReader.TryRead(response, out error)
        )
            return new UserInfoResult(error);

        throw new OpenIdProviderException(
            $"The UserInfo endpoint answered HTTP {response.StatusCode} without an OAuth error."
        );
    }

    private UserInfoResult ReadClaims(
        ProviderHttpResponse response,
        UserInfoRequest request,
        OpenIdProviderMetadata metadata
    )
    {
        if (
            string.Equals(
                response.MediaType,
                OpenIdProtocol.MediaTypes.Jwt,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return new UserInfoResult(
                UserInfoFailureCodes.UnsupportedResponse,
                "The UserInfo response is a signed or encrypted JWT, which the client does not validate."
            );

        if (
            response.Json is not { ValueKind: JsonValueKind.Object } claims
            || !claims.TryGetProperty(JoseClaimNames.Payload.Sub, out var sub)
            || sub.ValueKind != JsonValueKind.String
        )
            return new UserInfoResult(
                UserInfoFailureCodes.InvalidResponse,
                "The UserInfo response is not a JSON object with a string 'sub'."
            );

        // OpenID Connect Core 1.0 §5.3.2: a response about another subject must not be used.
        if (!string.Equals(sub.GetString(), request.ExpectedSubject, StringComparison.Ordinal))
            return new UserInfoResult(
                UserInfoFailureCodes.SubjectMismatch,
                "The UserInfo response is about another subject than the ID token."
            );

        var identity = ClaimsIdentityFactory.CreateClaimsIdentity(
            claims,
            AuthenticationType,
            JoseClaimNames.Payload.Name,
            JoseClaimNames.Payload.Role,
            metadata.Issuer
        );
        return new UserInfoResult(claims, identity);
    }
}
