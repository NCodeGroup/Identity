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

using Microsoft.AspNetCore.Http;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPProofValidator"/> abstraction (RFC 9449 §4.3). The
/// stateless checks — structure, signature, HTTP binding, freshness, and access-token hash — are the JOSE
/// <see cref="IDPoPProofService"/> building block's; this validator supplies the client's DPoP settings, maps a
/// rejection to <c>invalid_dpop_proof</c>, and adds the stateful server-nonce challenge and replay checks before
/// returning the RFC 7638 JWK thumbprint of the proof key.
/// </summary>
internal class DefaultDPoPProofValidator(
    IDPoPProofService proofService,
    IDPoPProofReplayCache replayCache,
    IDPoPNonceService nonceService
) : IDPoPProofValidator
{
    private IDPoPProofService ProofService { get; } = proofService;
    private IDPoPProofReplayCache ReplayCache { get; } = replayCache;
    private IDPoPNonceService NonceService { get; } = nonceService;

    /// <inheritdoc />
    public async ValueTask<string> ValidateAsync(
        OpenIdContext openIdContext,
        DPoPProofValidationRequest request,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = request.Settings;
        var lifetime = settings.GetValue(OpenIdSettingKeys.DPoPProofLifetime);

        var parameters = new DPoPProofValidationParameters
        {
            HttpMethod = request.HttpMethod,
            HttpUri = request.HttpUri,
            AllowedAlgorithms = settings.GetValue(OpenIdSettingKeys.DPoPSigningAlgValuesSupported),
            Lifetime = lifetime,
            AccessToken = request.AccessToken,
        };

        if (
            !ProofService.TryValidateProof(
                request.ProofJwt,
                parameters,
                out var proof,
                out var error
            )
        )
        {
            throw errorFactory
                .Create(OpenIdProtocol.ErrorCodes.InvalidDPoPProof)
                .WithDescription(error)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // When the server requires a nonce, the proof must carry a valid one; otherwise challenge the client with a
        // fresh nonce (RFC 9449 §8).
        if (settings.GetValue(OpenIdSettingKeys.DPoPNonceRequired))
        {
            var nonce = proof.Nonce;
            var nonceValid =
                !string.IsNullOrEmpty(nonce)
                && await NonceService.ValidateNonceAsync(openIdContext, nonce, cancellationToken);

            if (!nonceValid)
            {
                var freshNonce = await NonceService.IssueNonceAsync(
                    openIdContext,
                    cancellationToken
                );
                openIdContext.Http.Response.Headers[OpenIdConstants.DPoP.NonceHeaderName] =
                    freshNonce;

                throw errorFactory
                    .Create(OpenIdProtocol.ErrorCodes.UseDPoPNonce)
                    .WithDescription("The DPoP proof requires a server-provided nonce.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }
        }

        // Record the proof's jti to reject a replay within its acceptance window (RFC 9449 §11.1).
        var expiresWhen = proof.IssuedAt + lifetime;
        var recorded = await ReplayCache.TryRecordAsync(
            openIdContext,
            proof.JwkThumbprint,
            proof.Jti,
            expiresWhen,
            cancellationToken
        );
        if (!recorded)
        {
            throw errorFactory
                .Create(OpenIdProtocol.ErrorCodes.InvalidDPoPProof)
                .WithDescription("The DPoP proof has already been used.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return proof.JwkThumbprint;
    }
}
