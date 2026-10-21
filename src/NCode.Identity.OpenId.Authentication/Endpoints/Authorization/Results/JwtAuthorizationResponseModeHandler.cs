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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers a JWT-secured authorization response (JARM): the response parameters travel as the claims of one JWT —
/// issued by the tenant, addressed to the client, short-lived, signed with the client's
/// <c>authorization_signed_response_alg</c>, and encrypted when the client registers an encryption algorithm — in the
/// single <c>response</c> parameter of the derived response mode. An error response is secured the same way.
/// See ADR-0201.
/// </summary>
internal abstract class JwtAuthorizationResponseModeHandler(
    TimeProvider timeProvider,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IJwtEncoder jwtEncoder
) : IAuthorizationResponseModeHandler
{
    /// <summary>
    /// The lifetime of a response JWT: long enough for the redirect to complete, and short, because the response only
    /// has to survive one front-channel hop (JARM §2.1 recommends a short-lived JWT, ten minutes at most).
    /// </summary>
    internal static TimeSpan ResponseLifetime { get; } = TimeSpan.FromMinutes(5);

    private TimeProvider TimeProvider { get; } = timeProvider;

    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;

    private IJwtEncoder JwtEncoder { get; } = jwtEncoder;

    public abstract string ResponseMode { get; }

    public async ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    )
    {
        var openIdContext =
            result.OpenIdContext
            ?? throw new InvalidOperationException(
                "A JWT-secured authorization response requires the result's OpenIdContext."
            );
        var openIdClient =
            result.OpenIdClient
            ?? throw new InvalidOperationException(
                "A JWT-secured authorization response requires the result's OpenIdClient."
            );

        var credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
            openIdContext,
            openIdClient,
            ClientJwtProtectionSettings.Authorization,
            cancellationToken
        );

        if (credentials.SigningCredentials is null)
        {
            throw openIdContext
                .ErrorFactory.Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithDescription(
                    $"The client registers no '{OpenIdSettingNames.AuthorizationSignedResponseAlg}', and a JWT-secured authorization response is always signed."
                )
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .AsException();
        }

        // JARM §2.1: 'iss', 'aud', and 'exp' plus every response parameter. The issuer travels as the JWT's own 'iss'
        // claim, so the RFC 9207 'iss' response parameter is not repeated.
        var claims = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (name, values) in AuthorizationResponseWriter.GetParameters(message))
        {
            if (name == OpenIdConstants.Parameters.IssuerShort || values.Count == 0)
                continue;

            claims[name] = values.Count == 1 ? values.ToString() : values.ToArray();
        }

        var issuedAt = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = credentials.SigningCredentials,
            EncryptionCredentials = credentials.EncryptionCredentials,

            Issuer = openIdContext.Tenant.Issuer,
            Audience = openIdClient.ClientId,
            IssuedAt = issuedAt,
            Expires = issuedAt + ResponseLifetime,

            ExtraPayloadClaims = claims,
        };

        var responseJwt = JwtEncoder.Encode(parameters);

        await WriteAsync(
            httpContext.Response,
            result.RedirectUri,
            [
                KeyValuePair.Create(
                    OpenIdConstants.Parameters.Response,
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
        Uri redirectUri,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters,
        CancellationToken cancellationToken
    );
}
