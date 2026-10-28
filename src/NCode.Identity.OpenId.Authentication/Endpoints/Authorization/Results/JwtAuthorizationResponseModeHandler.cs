#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers a JWT-secured authorization response (JARM): the response parameters travel as the claims of one JWT —
/// issued by the tenant, addressed to the client, short-lived, signed with the client's
/// <c>authorization_signed_response_alg</c>, and encrypted when the client registers an encryption algorithm — in the
/// single <c>response</c> parameter of the derived response mode. An error response is secured the same way. The
/// credentials are the ones selected when the delivery was derived, before the user signed in, and travel on
/// <see cref="AuthorizationResponseDelivery.SecuringCredentials"/>; this handler selects none. See ADR-0201.
/// </summary>
internal abstract class JwtAuthorizationResponseModeHandler(
    TimeProvider timeProvider,
    IJwtEncoder jwtEncoder
) : IAuthorizationResponseModeHandler
{
    private TimeProvider TimeProvider { get; } = timeProvider;

    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;

    public abstract string ResponseMode { get; }

    public abstract string SecuredResponseMode { get; }

    public abstract bool DeliversInQuery { get; }

    public async ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = result.OpenIdContext;
        var openIdClient = result.OpenIdClient;

        var credentials =
            result.Delivery.SecuringCredentials
            ?? throw new InvalidOperationException(
                "A JWT-secured response is written only for a delivery that carries the credentials selected when it was derived."
            );

        var signingCredentials =
            credentials.SigningCredentials
            ?? throw new InvalidOperationException(
                "The credentials selected for a JWT-secured response always sign it."
            );

        // JARM §2.1: 'iss', 'aud', and 'exp' plus every response parameter. The issuer travels as the JWT's own 'iss'
        // claim, so the RFC 9207 'iss' response parameter is not repeated.
        var claims = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, values) in AuthorizationResponseWriter.GetParameters(message))
        {
            if (name == OpenIdProtocol.Parameters.IssuerShort || values.Count == 0)
                continue;

            claims[name] = values.Count == 1 ? values.ToString() : values.ToArray();
        }

        var issuedAt = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = signingCredentials,
            EncryptionCredentials = credentials.EncryptionCredentials,

            Issuer = openIdContext.Tenant.Issuer,
            Audience = openIdClient.ClientId,
            IssuedAt = issuedAt,
            Expires =
                issuedAt
                + openIdClient.Settings.GetValue(OpenIdSettingKeys.AuthorizationResponseLifetime),

            ExtraPayloadClaims = claims,
        };

        var responseJwt = JwtEncoder.Encode(parameters);

        await WriteAsync(
            httpContext.Response,
            result.Delivery.RedirectUri,
            [
                KeyValuePair.Create(
                    OpenIdProtocol.Parameters.Response,
                    new StringValues(responseJwt)
                ),
            ],
            cancellationToken
        );
    }

    /// <summary>
    /// Writes the <c>response</c> parameter in the response mode this JWT form derives from.
    /// </summary>
    protected abstract ValueTask WriteAsync(
        HttpResponse httpResponse,
        string redirectUri,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters,
        CancellationToken cancellationToken
    );
}
