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

using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.Jwt.Claims;
using NCode.Json;

namespace NCode.Identity.Jwt.Encoding;

/// <summary>
/// Provides a default implementation for the <see cref="IJwtEncoder"/> interface.
/// </summary>
/// <param name="optionsAccessor">An accessor that provides <see cref="JwtEncoderOptions"/>.</param>
/// <param name="timeProvider">A <see cref="TimeProvider"/> that can be used to get the current time.</param>
/// <param name="joseSerializer">An <see cref="IJoseSerializer"/> instance that provides the core <c>JOSE</c> implementation.</param>
internal sealed class DefaultJwtEncoder(
    IOptions<JwtEncoderOptions> optionsAccessor,
    TimeProvider timeProvider,
    IJoseSerializer joseSerializer
) : IJwtEncoder
{
    private JwtEncoderOptions Options { get; } = optionsAccessor.Value;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IJoseSerializer JoseSerializer { get; } = joseSerializer;

    /// <inheritdoc />
    public string Encode(JwtEncodingParameters parameters)
    {
        string? encodedToken = null;
        object valueToEncode = GetEffectivePayload(parameters);
        var isNested = false;

        if (parameters.SigningCredentials is not null)
        {
            var signingOptions = new JoseSigningOptions(
                new JoseSignatureOptions(parameters.SigningCredentials)
                {
                    TokenType = parameters.TokenType,
                    AddKeyIdHeader = parameters.AddKeyIdHeader,
                    ProtectedHeaders = parameters.ExtraSignatureHeaderClaims,
                }
            );

            encodedToken = JoseSerializer.CreateEncoder(signingOptions).Encode(valueToEncode);

            // the signed token becomes the content of the enclosing JWE, if any
            valueToEncode = encodedToken;
            isNested = true;
        }

        if (parameters.EncryptionCredentials is not null)
        {
            var encryptionCredentials = parameters.EncryptionCredentials;
            var recipient = new JoseRecipientOptions(
                new JoseKeyManagementCredentials(
                    encryptionCredentials.SecretKey,
                    encryptionCredentials.KeyManagementAlgorithm
                )
            )
            {
                AddKeyIdHeader = parameters.AddKeyIdHeader,
            };

            var encryptingOptions = new JoseEncryptionOptions(
                encryptionCredentials.AuthenticatedEncryptionAlgorithm
            )
            {
                CompressionAlgorithm = encryptionCredentials.CompressionAlgorithm,
                TokenType = parameters.TokenType,
                ProtectedHeaders = GetEncryptionHeaders(parameters, isNested),
                Recipients = [recipient],
            };

            encodedToken = JoseSerializer.CreateEncoder(encryptingOptions).Encode(valueToEncode);
        }

        if (encodedToken == null)
            throw new ArgumentException(
                "Both SigningOptions and EncryptingOptions cannot be null.",
                nameof(parameters)
            );

        return encodedToken;
    }

    // RFC 7519 §5.2: the JWE that encloses a signed JWT (a Nested JWT) must carry a 'cty' of 'JWT', which is how a
    // recipient knows its content is itself a JWT to validate.
    private static IReadOnlyDictionary<string, object>? GetEncryptionHeaders(
        JwtEncodingParameters parameters,
        bool isNested
    )
    {
        var extraHeaders = parameters.ExtraEncryptionHeaderClaims;
        if (!isNested)
            return extraHeaders;

        var headers = extraHeaders is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(extraHeaders, StringComparer.Ordinal);

        headers[JoseClaimNames.Header.Cty] = JoseTokenTypes.Jwt;

        return headers;
    }

    private Dictionary<string, object> GetEffectivePayload(JwtEncodingParameters parameters)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);

        ProcessSubjectClaims(payload, parameters.SubjectClaims);

        ProcessExtraPayloadClaims(payload, parameters.ExtraPayloadClaims);

        ProcessRequestClaims(payload, parameters);

        ProcessTokenLifetime(payload);

        return payload;
    }

    private static void ProcessSubjectClaims(
        Dictionary<string, object> payload,
        IEnumerable<Claim>? subjectClaims
    )
    {
        if (subjectClaims is null)
            return;

        foreach (var claim in subjectClaims)
        {
            var nativeValue = ClaimValueMapper.ToJsonValue(claim);

            // ClaimsIdentity may contain duplicate claims. If we encounter a duplicate claim,
            // we need to convert the duplicate values into a collection.

            if (payload.TryGetValue(claim.Type, out var existingValue))
            {
                if (existingValue is ICollection<object> existingCollection)
                {
                    existingCollection.Add(nativeValue);
                }
                else
                {
                    payload[claim.Type] = new HashSet<object> { existingValue, nativeValue };
                }
            }
            else
            {
                payload[claim.Type] = nativeValue;
            }
        }
    }

    private static void ProcessExtraPayloadClaims(
        Dictionary<string, object> payload,
        IReadOnlyDictionary<string, object>? extraClaims
    )
    {
        if (extraClaims is null)
            return;

        foreach (var (key, value) in extraClaims)
        {
            payload[key] = value;
        }
    }

    private static void ProcessRequestClaims(
        Dictionary<string, object> payload,
        JwtEncodingParameters parameters
    )
    {
        if (!string.IsNullOrEmpty(parameters.Issuer))
        {
            payload[JoseClaimNames.Payload.Iss] = parameters.Issuer;
        }

        if (!StringValues.IsNullOrEmpty(parameters.Audience))
        {
            payload[JoseClaimNames.Payload.Aud] =
                parameters.Audience.Count == 1
                    ? parameters.Audience.ToString()
                    : parameters.Audience.ToArray();
        }

        if (parameters.IssuedAt.HasValue)
        {
            payload[JoseClaimNames.Payload.Iat] = parameters.IssuedAt.Value.ToUnixTimeSeconds();
        }

        if (parameters.NotBefore.HasValue)
        {
            payload[JoseClaimNames.Payload.Nbf] = parameters.NotBefore.Value.ToUnixTimeSeconds();
        }

        if (parameters.Expires.HasValue)
        {
            payload[JoseClaimNames.Payload.Exp] = parameters.Expires.Value.ToUnixTimeSeconds();
        }
    }

    private void ProcessTokenLifetime(Dictionary<string, object> payload)
    {
        if (!Options.EnsureTokenLifetime)
            return;

        DateTimeOffset issuedAt;
        if (
            payload.TryGetValue<long>(JoseClaimNames.Payload.Iat, out var iat)
            && DateCoercion.TryFromNumericDate(iat, out var suppliedIssuedAt)
        )
        {
            issuedAt = suppliedIssuedAt;
        }
        else
        {
            issuedAt = TimeProvider.GetUtcNowWithPrecisionInSeconds();
            payload[JoseClaimNames.Payload.Iat] = issuedAt.ToUnixTimeSeconds();
        }

        if (!payload.ContainsKey(JoseClaimNames.Payload.Nbf))
        {
            payload[JoseClaimNames.Payload.Nbf] = issuedAt.ToUnixTimeSeconds();
        }

        if (!payload.ContainsKey(JoseClaimNames.Payload.Exp))
        {
            var expires = issuedAt + Options.DefaultTokenLifetime;
            payload[JoseClaimNames.Payload.Exp] = expires.ToUnixTimeSeconds();
        }
    }
}
