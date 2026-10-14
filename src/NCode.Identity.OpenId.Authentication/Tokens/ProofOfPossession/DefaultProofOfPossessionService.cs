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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IProofOfPossessionService"/> abstraction, binding issued
/// access tokens to a DPoP proof key (RFC 9449) and/or a mutual-TLS client certificate (RFC 8705).
/// </summary>
internal class DefaultProofOfPossessionService(
    IDPoPProofValidator dpopProofValidator,
    IClientCertificateResolver clientCertificateResolver
) : IProofOfPossessionService
{
    private IDPoPProofValidator DPoPProofValidator { get; } = dpopProofValidator;
    private IClientCertificateResolver ClientCertificateResolver { get; } =
        clientCertificateResolver;

    /// <inheritdoc />
    public async ValueTask<ProofOfPossessionBinding> EvaluateTokenRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CancellationToken cancellationToken
    )
    {
        var httpContext = openIdContext.Http;
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var jwkThumbprint = await EvaluateDPoPAsync(
            openIdContext,
            openIdClient,
            httpContext,
            cancellationToken
        );

        var certificateThumbprint = await EvaluateMutualTlsAsync(
            httpContext,
            settings.GetValue(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens),
            errorFactory,
            cancellationToken
        );

        var confirmation = new TokenConfirmation
        {
            JwkThumbprint = jwkThumbprint,
            X509CertificateSha256Thumbprint = certificateThumbprint,
        };

        // A DPoP-bound token advertises token_type=DPoP; a purely certificate-bound (or unbound) token stays Bearer.
        var tokenType = jwkThumbprint is not null
            ? OpenIdConstants.TokenTypes.DPoP
            : OpenIdConstants.TokenTypes.Bearer;

        return new ProofOfPossessionBinding { TokenType = tokenType, Confirmation = confirmation };
    }

    /// <inheritdoc />
    public async ValueTask<IOpenIdError?> ValidateResourceAccessAsync(
        OpenIdContext openIdContext,
        string accessToken,
        TokenConfirmation confirmation,
        CancellationToken cancellationToken
    )
    {
        if (!confirmation.HasValue)
            return null;

        var errorFactory = openIdContext.ErrorFactory;
        var httpContext = openIdContext.Http;
        var request = httpContext.Request;

        if (!string.IsNullOrEmpty(confirmation.JwkThumbprint))
        {
            // A DPoP-bound token MUST be presented with the DPoP authentication scheme (RFC 9449 §7.1).
            var authorization = request.Headers.Authorization.ToString();
            if (
                !authorization.StartsWith(
                    $"{OpenIdConstants.TokenTypes.DPoP} ",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return InvalidToken(
                    errorFactory,
                    "A DPoP-bound access token must be presented with the 'DPoP' authentication scheme."
                );
            }

            var proofs = request.Headers[OpenIdConstants.DPoP.HeaderName];
            var proofJwt = proofs.Count == 1 ? proofs[0] : null;
            if (string.IsNullOrEmpty(proofJwt))
            {
                return errorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidDPoPProof)
                    .WithDescription(
                        "A DPoP proof is required to present a DPoP-bound access token."
                    )
                    .WithStatusCode(StatusCodes.Status401Unauthorized);
            }

            var httpUri = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
            string proofJwkThumbprint;
            try
            {
                proofJwkThumbprint = await DPoPProofValidator.ValidateAsync(
                    openIdContext,
                    new DPoPProofValidationRequest
                    {
                        ProofJwt = proofJwt,
                        HttpMethod = request.Method,
                        HttpUri = httpUri,
                        Settings = openIdContext.Tenant.SettingsProvider.Collection,
                        AccessToken = accessToken,
                    },
                    cancellationToken
                );
            }
            catch (OpenIdException exception)
            {
                return exception.Error;
            }

            if (
                !string.Equals(
                    proofJwkThumbprint,
                    confirmation.JwkThumbprint,
                    StringComparison.Ordinal
                )
            )
            {
                return errorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidDPoPProof)
                    .WithDescription(
                        "The DPoP proof key does not match the access token's confirmation."
                    )
                    .WithStatusCode(StatusCodes.Status401Unauthorized);
            }
        }

        if (!string.IsNullOrEmpty(confirmation.X509CertificateSha256Thumbprint))
        {
            var certificate = await ClientCertificateResolver.ResolveCertificateAsync(
                httpContext,
                cancellationToken
            );
            if (certificate is null)
            {
                return InvalidToken(
                    errorFactory,
                    "A client certificate is required to present a certificate-bound access token."
                );
            }

            var thumbprint = Base64Url.Encode(SHA256.HashData(certificate.RawData));
            if (
                !string.Equals(
                    thumbprint,
                    confirmation.X509CertificateSha256Thumbprint,
                    StringComparison.Ordinal
                )
            )
            {
                return InvalidToken(
                    errorFactory,
                    "The client certificate does not match the access token's confirmation."
                );
            }
        }

        return null;
    }

    private static IOpenIdError InvalidToken(
        IOpenIdErrorFactory errorFactory,
        string description
    ) =>
        errorFactory
            .InvalidToken()
            .WithDescription(description)
            .WithStatusCode(StatusCodes.Status401Unauthorized);

    private async ValueTask<string?> EvaluateDPoPAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var request = httpContext.Request;
        var proofs = request.Headers[OpenIdConstants.DPoP.HeaderName];

        // A request carries at most one DPoP proof (RFC 9449 §4.1).
        if (proofs.Count > 1)
        {
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidDPoPProof)
                .WithDescription("The request contains more than one DPoP proof.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        var proofJwt = proofs.Count == 1 ? proofs[0] : null;
        var requireDPoP = openIdClient.Settings.GetValue(OpenIdSettingKeys.RequireDPoP);

        if (string.IsNullOrEmpty(proofJwt))
        {
            if (requireDPoP)
            {
                throw errorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidDPoPProof)
                    .WithDescription("A DPoP proof is required but was not presented.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }

            return null;
        }

        var httpUri = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";

        return await DPoPProofValidator.ValidateAsync(
            openIdContext,
            new DPoPProofValidationRequest
            {
                ProofJwt = proofJwt,
                HttpMethod = request.Method,
                HttpUri = httpUri,
                Settings = openIdClient.Settings,
            },
            cancellationToken
        );
    }

    private async ValueTask<string?> EvaluateMutualTlsAsync(
        HttpContext httpContext,
        bool certificateBindingRequired,
        IOpenIdErrorFactory errorFactory,
        CancellationToken cancellationToken
    )
    {
        if (!certificateBindingRequired)
            return null;

        var certificate = await ClientCertificateResolver.ResolveCertificateAsync(
            httpContext,
            cancellationToken
        );

        if (certificate is null)
        {
            throw errorFactory
                .InvalidRequest(
                    "A client certificate is required for certificate-bound access tokens."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return Base64Url.Encode(SHA256.HashData(certificate.RawData));
    }
}
