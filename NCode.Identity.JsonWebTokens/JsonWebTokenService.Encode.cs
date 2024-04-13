#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NCode.Jose;
using NCode.Jose.Buffers;
using NCode.Jose.Extensions;

namespace NCode.Identity.JsonWebTokens;

partial class JsonWebTokenService
{
    /// <inheritdoc />
    public string EncodeJwt(EncodeJwtParameters parameters)
    {
        string? encodedToken = null;
        object valueToEncode = GetEffectivePayload(parameters);

        if (parameters.SigningCredentials is not null)
        {
            var signingOptions = new JoseSigningOptions(parameters.SigningCredentials)
            {
                TokenType = parameters.TokenType,
                AddKeyIdHeader = parameters.AddKeyIdHeader
            };

            encodedToken = JoseSerializer.Encode(
                valueToEncode,
                signingOptions,
                extraHeaders: parameters.ExtraSignatureHeaderClaims);

            // for the possibility of a nested token
            valueToEncode = encodedToken;
        }

        if (parameters.EncryptionCredentials is not null)
        {
            var encryptingOptions = new JoseEncryptionOptions(parameters.EncryptionCredentials)
            {
                TokenType = parameters.TokenType,
                AddKeyIdHeader = parameters.AddKeyIdHeader
            };

            encodedToken = JoseSerializer.Encode(
                valueToEncode,
                encryptingOptions,
                extraHeaders: parameters.ExtraEncryptionHeaderClaims);
        }

        if (encodedToken == null)
            throw new ArgumentException("Both SigningOptions and EncryptingOptions cannot be null.", nameof(parameters));

        return encodedToken;
    }

    private IDictionary<string, object> GetEffectivePayload(EncodeJwtParameters parameters)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);

        ProcessSubjectClaims(payload, parameters.SubjectClaims);

        ProcessExtraPayloadClaims(payload, parameters.ExtraPayloadClaims);

        ProcessRequestClaims(payload, parameters);

        ProcessTokenLifetime(payload);

        return payload;
    }

    private static void ProcessSubjectClaims(
        IDictionary<string, object> payload,
        IEnumerable<Claim>? subjectClaims)
    {
        if (subjectClaims is null) return;

        foreach (var claim in subjectClaims)
        {
            var nativeValue = ConvertClaimToNativeValue(claim);

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
                    payload[claim.Type] = new HashSet<object>
                    {
                        existingValue,
                        nativeValue
                    };
                }
            }
            else
            {
                payload[claim.Type] = nativeValue;
            }
        }
    }

    private static void ProcessExtraPayloadClaims(
        IDictionary<string, object> payload,
        IReadOnlyDictionary<string, object>? extraClaims)
    {
        if (extraClaims is not { Count: > 1 }) return;

        foreach (var (key, value) in extraClaims)
        {
            payload[key] = value;
        }
    }

    private static void ProcessRequestClaims(
        IDictionary<string, object> payload,
        EncodeJwtParameters parameters)
    {
        if (!string.IsNullOrEmpty(parameters.Issuer))
        {
            payload[JoseClaimNames.Payload.Iss] = parameters.Issuer;
        }

        if (!StringValues.IsNullOrEmpty(parameters.Audience))
        {
            payload[JoseClaimNames.Payload.Aud] = parameters.Audience.Count == 1 ?
                parameters.Audience.ToString() :
                parameters.Audience.ToArray();
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

    private void ProcessTokenLifetime(IDictionary<string, object> payload)
    {
        if (!Options.EnsureTokenLifetime) return;

        DateTimeOffset issuedAt;
        if (payload.TryGetValue<long>(JoseClaimNames.Payload.Iat, out var iat))
        {
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
        }
        else
        {
            issuedAt = SystemClock.UtcNow;
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

    private static object ConvertClaimToNativeValue(Claim claim) =>
        claim.ValueType switch
        {
            ClaimValueTypes.String => claim.Value,
            ClaimValueTypes.Boolean when bool.TryParse(
                claim.Value,
                out var boolValue) => boolValue,
            ClaimValueTypes.Double when double.TryParse(
                claim.Value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var doubleValue) => doubleValue,
            ClaimValueTypes.Integer or ClaimValueTypes.Integer32 when int.TryParse(
                claim.Value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var intValue) => intValue,
            ClaimValueTypes.Integer64 when long.TryParse(
                claim.Value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var longValue) => longValue,
            ClaimValueTypes.DateTime when DateTimeOffset.TryParse(
                claim.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateTimeOffsetValue) => dateTimeOffsetValue,
            ClaimValueTypes.DateTime when DateTime.TryParse(
                claim.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateTimeValue) => dateTimeValue.ToUniversalTime(),
            JsonClaimValueTypes.Json => CreateJsonElement(claim.Value),
            JsonClaimValueTypes.JsonArray => CreateJsonElement(claim.Value),
            JsonClaimValueTypes.JsonNull => string.Empty,
            _ => claim.Value
        };

    private static JsonElement CreateJsonElement(string json)
    {
        var byteCount = Encoding.UTF8.GetByteCount(json);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> buffer);

        var bytesWritten = Encoding.UTF8.GetBytes(json, buffer);
        Debug.Assert(bytesWritten == byteCount);

        var reader = new Utf8JsonReader(buffer);
        return JsonElement.TryParseValue(ref reader, out var jsonElement) ?
            jsonElement.Value :
            default;
    }
}
