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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Provides the default implementation of the <see cref="IDPoPProofValidator"/> abstraction (RFC 9449 §4.3). A DPoP
/// proof is a compact JWS signed by a public key embedded in its own protected header; this validator checks the
/// proof's structure, signature, HTTP binding, freshness, replay, and optional server nonce, then returns the
/// RFC 7638 JWK thumbprint of the proof key.
/// </summary>
internal class DefaultDPoPProofValidator(
    TimeProvider timeProvider,
    IEccCurveCatalog eccCurveCatalog,
    IDPoPProofReplayCache replayCache,
    IDPoPNonceService nonceService
) : IDPoPProofValidator
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;
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
        var proofJwt = request.ProofJwt;

        OpenIdException Invalid(string description) =>
            errorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidDPoPProof)
                .WithDescription(description)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // A DPoP proof is a compact JWS: header.payload.signature (RFC 9449 §4.1).
        var parts = proofJwt.Split('.');
        if (parts.Length != 3)
            throw Invalid("The DPoP proof is not a well-formed compact JWS.");

        JsonElement header;
        JsonElement payload;
        try
        {
            using var headerDoc = JsonDocument.Parse(DecodeBase64Url(parts[0]));
            using var payloadDoc = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            header = headerDoc.RootElement.Clone();
            payload = payloadDoc.RootElement.Clone();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw Invalid("The DPoP proof header or payload could not be decoded.");
        }

        // typ MUST be "dpop+jwt" (RFC 9449 §4.2).
        if (
            !TryGetString(header, JoseClaimNames.Header.Typ, out var typ)
            || !string.Equals(typ, OpenIdConstants.DPoP.ProofJwtType, StringComparison.Ordinal)
        )
        {
            throw Invalid("The DPoP proof 'typ' header must be 'dpop+jwt'.");
        }

        // alg MUST be an asymmetric algorithm the server accepts; never 'none' or a symmetric algorithm.
        if (
            !TryGetString(header, JoseClaimNames.Header.Alg, out var alg)
            || string.IsNullOrEmpty(alg)
        )
            throw Invalid("The DPoP proof is missing the 'alg' header.");

        var allowedAlgorithms = settings.GetValue(OpenIdSettingKeys.DPoPSigningAlgValuesSupported);
        if (!allowedAlgorithms.Contains(alg, StringComparer.Ordinal))
            throw Invalid($"The DPoP proof 'alg' header '{alg}' is not supported.");

        // The embedded public JWK is the key the proof is signed with and the token is bound to; it MUST NOT carry
        // any private key material (RFC 9449 §4.3).
        if (
            !header.TryGetProperty(JoseClaimNames.Header.Jwk, out var jwkElement)
            || jwkElement.ValueKind != JsonValueKind.Object
        )
        {
            throw Invalid("The DPoP proof is missing the 'jwk' header.");
        }

        JsonWebKey jwk;
        try
        {
            jwk = new JsonWebKey(jwkElement);
        }
        catch (ArgumentException)
        {
            throw Invalid("The DPoP proof 'jwk' header is not a valid JSON Web Key.");
        }

        if (jwk.ContainsPrivateKeyMaterial)
            throw Invalid("The DPoP proof 'jwk' header must not contain private key material.");

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] signature;
        try
        {
            signature = DecodeBase64Url(parts[2]);
        }
        catch (FormatException)
        {
            throw Invalid("The DPoP proof signature could not be decoded.");
        }

        var jwkThumbprint = VerifySignatureAndComputeThumbprint(
            jwk,
            alg,
            signingInput,
            signature,
            Invalid
        );

        // htm MUST equal the request method (RFC 9449 §4.3).
        if (
            !TryGetString(payload, OpenIdConstants.DPoP.HttpMethodClaim, out var htm)
            || !string.Equals(htm, request.HttpMethod, StringComparison.Ordinal)
        )
        {
            throw Invalid("The DPoP proof 'htm' claim does not match the request method.");
        }

        // htu MUST equal the request URI with any query and fragment removed (RFC 9449 §4.3).
        if (
            !TryGetString(payload, OpenIdConstants.DPoP.HttpUriClaim, out var htu)
            || !IsSameHttpUri(htu, request.HttpUri)
        )
        {
            throw Invalid("The DPoP proof 'htu' claim does not match the request URI.");
        }

        // ath, when the proof accompanies an access token at a protected resource, MUST equal the base64url SHA-256
        // hash of that access token (RFC 9449 §4.3, §7.1).
        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            var expectedAth = Base64Url.Encode(
                SHA256.HashData(Encoding.ASCII.GetBytes(request.AccessToken))
            );
            if (
                !TryGetString(payload, OpenIdConstants.DPoP.AccessTokenHashClaim, out var ath)
                || !string.Equals(ath, expectedAth, StringComparison.Ordinal)
            )
            {
                throw Invalid("The DPoP proof 'ath' claim does not match the access token.");
            }
        }

        // jti MUST be present so the proof can be tracked for replay (RFC 9449 §4.3).
        if (
            !TryGetString(payload, JoseClaimNames.Payload.Jti, out var jti)
            || string.IsNullOrEmpty(jti)
        )
            throw Invalid("The DPoP proof is missing the 'jti' claim.");

        // iat MUST be present and within the acceptable freshness window (RFC 9449 §4.3, §11.1).
        if (
            !payload.TryGetProperty(JoseClaimNames.Payload.Iat, out var iatElement)
            || iatElement.ValueKind != JsonValueKind.Number
            || !iatElement.TryGetInt64(out var iatSeconds)
        )
        {
            throw Invalid("The DPoP proof is missing a valid 'iat' claim.");
        }

        var lifetime = settings.GetValue(OpenIdSettingKeys.DPoPProofLifetime);
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds);
        var now = TimeProvider.GetUtcNow();
        if (issuedAt > now + lifetime || issuedAt < now - lifetime)
            throw Invalid("The DPoP proof 'iat' claim is outside the acceptable window.");

        // When the server requires a nonce, the proof must carry a valid one; otherwise challenge the client with a
        // fresh nonce (RFC 9449 §8).
        if (settings.GetValue(OpenIdSettingKeys.DPoPNonceRequired))
        {
            var hasNonce = TryGetString(payload, JoseClaimNames.Payload.Nonce, out var nonce);
            var nonceValid =
                hasNonce
                && !string.IsNullOrEmpty(nonce)
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
                    .Create(OpenIdConstants.ErrorCodes.UseDPoPNonce)
                    .WithDescription("The DPoP proof requires a server-provided nonce.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
            }
        }

        // Record the proof's jti to reject a replay within its acceptance window (RFC 9449 §11.1).
        var expiresWhen = issuedAt + lifetime;
        var recorded = await ReplayCache.TryRecordAsync(
            jwkThumbprint,
            jti,
            expiresWhen,
            cancellationToken
        );
        if (!recorded)
            throw Invalid("The DPoP proof has already been used.");

        return jwkThumbprint;
    }

    private string VerifySignatureAndComputeThumbprint(
        JsonWebKey jwk,
        string alg,
        byte[] signingInput,
        byte[] signature,
        Func<string, OpenIdException> invalid
    )
    {
        var kty = jwk.KeyType;

        var hashAlgorithmName = alg[^3..] switch
        {
            "256" => HashAlgorithmName.SHA256,
            "384" => HashAlgorithmName.SHA384,
            "512" => HashAlgorithmName.SHA512,
            _ => throw invalid($"The DPoP proof 'alg' header '{alg}' is not supported."),
        };

        switch (kty)
        {
            case "RSA":
            {
                if (
                    jwk.GetMember("n") is not { Length: > 0 } modulus
                    || jwk.GetMember("e") is not { Length: > 0 } exponent
                )
                {
                    throw invalid("The DPoP proof 'jwk' is missing RSA public members.");
                }

                var padding = alg.StartsWith("PS", StringComparison.Ordinal)
                    ? RSASignaturePadding.Pss
                    : RSASignaturePadding.Pkcs1;

                using var rsa = RSA.Create();
                try
                {
                    rsa.ImportParameters(
                        new RSAParameters
                        {
                            Modulus = DecodeBase64Url(modulus),
                            Exponent = DecodeBase64Url(exponent),
                        }
                    );
                }
                catch (Exception exception)
                    when (exception is FormatException or CryptographicException)
                {
                    throw invalid("The DPoP proof 'jwk' RSA public key is invalid.");
                }

                if (!rsa.VerifyData(signingInput, signature, hashAlgorithmName, padding))
                    throw invalid("The DPoP proof signature is invalid.");

                break;
            }

            case "EC":
            {
                if (
                    jwk.GetMember("crv") is not { Length: > 0 } curveName
                    || jwk.GetMember("x") is not { Length: > 0 } x
                    || jwk.GetMember("y") is not { Length: > 0 } y
                )
                {
                    throw invalid("The DPoP proof 'jwk' is missing EC public members.");
                }

                if (!EccCurveCatalog.TryGetByName(curveName, out var curveSpecification))
                    throw invalid($"The DPoP proof 'jwk' curve '{curveName}' is not supported.");

                using var ecdsa = ECDsa.Create();
                try
                {
                    ecdsa.ImportParameters(
                        new ECParameters
                        {
                            Curve = curveSpecification.Curve,
                            Q = new ECPoint { X = DecodeBase64Url(x), Y = DecodeBase64Url(y) },
                        }
                    );
                }
                catch (Exception exception)
                    when (exception is FormatException or CryptographicException)
                {
                    throw invalid("The DPoP proof 'jwk' EC public key is invalid.");
                }

                // JWS ECDSA signatures are the raw r||s concatenation (IEEE P1363), not DER.
                if (
                    !ecdsa.VerifyData(
                        signingInput,
                        signature,
                        hashAlgorithmName,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                    )
                )
                {
                    throw invalid("The DPoP proof signature is invalid.");
                }

                break;
            }

            default:
                throw invalid($"The DPoP proof 'jwk' key type '{kty}' is not supported.");
        }

        // RFC 7638 / RFC 9278: the thumbprint is computed over the key type's required members in canonical form.
        try
        {
            return jwk.ComputeThumbprint();
        }
        catch (Exception exception)
            when (exception is NotSupportedException or InvalidOperationException)
        {
            throw invalid(
                "The DPoP proof 'jwk' is missing members required to compute its thumbprint."
            );
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (
            element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
        )
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var byteCount = Base64Url.GetByteCountForDecode(value.Length);
        var buffer = new byte[byteCount];
        if (!Base64Url.TryDecode(value, buffer, out var bytesWritten))
            throw new FormatException("The value is not valid base64url.");
        return bytesWritten == byteCount ? buffer : buffer[..bytesWritten];
    }

    private static bool IsSameHttpUri(string? proofHtu, string requestHtu)
    {
        if (
            string.IsNullOrEmpty(proofHtu)
            || !Uri.TryCreate(proofHtu, UriKind.Absolute, out var proofUri)
            || !Uri.TryCreate(requestHtu, UriKind.Absolute, out var requestUri)
        )
        {
            return false;
        }

        // Compare scheme, authority, and path, ignoring query and fragment (RFC 9449 §4.3).
        return string.Equals(
            proofUri.GetLeftPart(UriPartial.Path),
            requestUri.GetLeftPart(UriPartial.Path),
            StringComparison.Ordinal
        );
    }
}
