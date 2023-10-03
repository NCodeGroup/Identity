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
using NCode.CryptoMemory;
using NCode.Identity.Jwt;
using NCode.Jose;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Credentials;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

// JWT (JWS, JWE, JWS+JWE)
// Reference

public class EncodeSecurityTokenRequest
{
    public bool IsNestedToken => SigningOptions is not null && EncryptingOptions is not null;

    public JoseSigningOptions? SigningOptions { get; set; }

    public JoseEncryptingOptions? EncryptingOptions { get; set; }

    //

    public string? Issuer { get; set; }

    public IList<string>? Audiences { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }

    public DateTimeOffset? NotBefore { get; set; }

    public DateTimeOffset? Expires { get; set; }

    public TimeSpan? Lifetime { get; set; }

    //

    public ClaimsIdentity? Subject { get; set; }

    public IDictionary<string, object>? ExtraPayloadClaims { get; set; }

    public IDictionary<string, object>? ExtraSignatureHeaderClaims { get; set; }

    public IDictionary<string, object>? ExtraEncryptionHeaderClaims { get; set; }
}

public class EncodeSecurityTokenResponse
{
    public EncodeSecurityTokenRequest Request { get; }

    public string EncodedToken { get; set; }
}

public class SecurityTokenService
{
    private static JoseSigningOptions NullSigningOptions { get; } = new(new JoseSigningCredentials(
        EmptySecretKey.Singleton,
        NoneSignatureAlgorithm.Singleton));

    private IJoseSerializer JoseSerializer { get; }

    private bool EnsureTokenLifetime { get; set; } = true;
    private TimeSpan DefaultTokenLifetime { get; } = TimeSpan.FromMinutes(60);

    public SecurityTokenService(IJoseSerializer joseSerializer)
    {
        JoseSerializer = joseSerializer;
    }

    public EncodeSecurityTokenResponse EncodeSecurityToken(EncodeSecurityTokenRequest request)
    {
        if (request.SigningOptions is null && request.EncryptingOptions is null)
            throw new ArgumentException("Both SigningOptions and EncryptingOptions cannot be omitted.", nameof(request));

        if (request.SigningOptions is not null)
        {
            var signedToken = SignToken(request);
        }

        throw new NotImplementedException();
    }

    private string SignToken(EncodeSecurityTokenRequest request)
    {
        Debug.Assert(request.SigningOptions != null);

        var payload = GetEffectivePayload(request);

        var encodedToken = JoseSerializer.Encode(
            payload,
            request.SigningOptions,
            extraHeaders: request.ExtraSignatureHeaderClaims);

        return encodedToken;
    }

    private IDictionary<string, object> GetEffectivePayload(EncodeSecurityTokenRequest request)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);

        var extraClaims = request.ExtraPayloadClaims;
        var hasExtraClaims = extraClaims is { Count: > 0 };

        var issuer = request.Issuer;
        var hasIssuer = !string.IsNullOrEmpty(issuer);
        if (hasIssuer)
            payload[JoseClaimNames.Payload.Iss] = issuer!;

        var audiences = request.Audiences;
        var hasAudiences = audiences is { Count: > 0 };
        if (hasAudiences)
        {
            payload[JoseClaimNames.Payload.Aud] = request.Audiences!.Count == 1 ?
                request.Audiences[0] :
                request.Audiences;
        }

        var issuedAt = request.IssuedAt;
        var hasIssuedAt = issuedAt.HasValue;
        if (hasIssuedAt)
            payload[JoseClaimNames.Payload.Iat] = issuedAt!.Value.ToUnixTimeSeconds();

        var notBefore = request.NotBefore;
        var hasNotBefore = notBefore.HasValue;
        if (hasNotBefore)
            payload[JoseClaimNames.Payload.Nbf] = notBefore!.Value.ToUnixTimeSeconds();

        var expires = request.Expires;
        var hasExpires = expires.HasValue;
        if (hasExpires)
            payload[JoseClaimNames.Payload.Exp] = expires!.Value.ToUnixTimeSeconds();

        if (request.Subject is not null)
        {
            foreach (var claim in request.Subject.Claims)
            {
                if (hasExtraClaims && extraClaims!.ContainsKey(claim.Type))
                    continue;

                if (JoseClaimNames.Payload.Iss.Equals(claim.Type, StringComparison.Ordinal))
                {
                    if (hasIssuer) continue;
                    hasIssuer = true;
                }

                if (JoseClaimNames.Payload.Aud.Equals(claim.Type, StringComparison.Ordinal))
                {
                    if (hasAudiences) continue;
                    hasAudiences = true;
                }

                if (JoseClaimNames.Payload.Iat.Equals(claim.Type, StringComparison.Ordinal))
                {
                    if (hasIssuedAt) continue;

                    if (ClaimValueTypes.DateTime.Equals(claim.ValueType, StringComparison.Ordinal) &&
                        DateTimeOffset.TryParse(claim.Value, out var dateTimeOffsetValue))
                    {
                        issuedAt = dateTimeOffsetValue;
                    }
                    else if (ClaimValueTypes.Integer64.Equals(claim.ValueType, StringComparison.Ordinal) &&
                             long.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var longValue))
                    {
                        issuedAt = DateTimeOffset.FromUnixTimeSeconds(longValue);
                    }

                    hasIssuedAt = issuedAt.HasValue;
                }

                if (JoseClaimNames.Payload.Nbf.Equals(claim.Type, StringComparison.Ordinal))
                {
                    if (hasNotBefore) continue;
                    hasNotBefore = true;
                }

                if (JoseClaimNames.Payload.Exp.Equals(claim.Type, StringComparison.Ordinal))
                {
                    if (hasExpires) continue;
                    hasExpires = true;
                }

                var nativeValue = ConvertClaimToNativeValue(claim);

                // ClaimsIdentity may contain duplicate claims. If we encounter a duplicate claim,
                // we need to convert the values to a collection.

                if (payload.TryGetValue(claim.Type, out var existingValue))
                {
                    if (existingValue is ICollection<object> existingCollection)
                    {
                        existingCollection.Add(nativeValue);
                    }
                    else
                    {
                        payload[claim.Type] = new List<object>
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

        if (request.ExtraPayloadClaims is { Count: > 1 })
        {
            foreach (var (key, value) in request.ExtraPayloadClaims)
            {
                if (JoseClaimNames.Payload.Iss.Equals(key, StringComparison.Ordinal))
                {
                    if (hasIssuer) continue;
                    hasIssuer = true;
                }

                if (JoseClaimNames.Payload.Aud.Equals(key, StringComparison.Ordinal))
                {
                    if (hasAudiences) continue;
                    hasAudiences = true;
                }

                if (JoseClaimNames.Payload.Iat.Equals(key, StringComparison.Ordinal))
                {
                    if (hasIssuedAt) continue;
                    issuedAt = value switch
                    {
                        DateTime dateTimeValue => dateTimeValue.ToUniversalTime(),
                        DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue,
                        long longValue => DateTimeOffset.FromUnixTimeSeconds(longValue),
                        string stringValue when DateTimeOffset.TryParse(stringValue, out var dateTimeOffsetValue) => dateTimeOffsetValue,
                        _ => issuedAt
                    };
                    hasIssuedAt = issuedAt.HasValue;
                }

                if (JoseClaimNames.Payload.Nbf.Equals(key, StringComparison.Ordinal))
                {
                    if (hasNotBefore) continue;
                    hasNotBefore = true;
                }

                if (JoseClaimNames.Payload.Exp.Equals(key, StringComparison.Ordinal))
                {
                    if (hasExpires) continue;
                    hasExpires = true;
                }

                payload[key] = value;
            }
        }

        if (EnsureTokenLifetime)
        {
            // TODO: use clock
            var utcNow = DateTimeOffset.UtcNow;

            if (!hasIssuedAt)
            {
                issuedAt = utcNow;
                payload[JoseClaimNames.Payload.Iat] = issuedAt.Value.ToUnixTimeSeconds();
            }

            if (!hasNotBefore)
            {
                notBefore = issuedAt;
                payload[JoseClaimNames.Payload.Nbf] = notBefore!.Value.ToUnixTimeSeconds();
            }

            if (!hasExpires)
            {
                expires = issuedAt!.Value.Add(request.Lifetime ?? DefaultTokenLifetime);
                payload[JoseClaimNames.Payload.Exp] = expires.Value.ToUnixTimeSeconds();
            }
        }

        return payload;
    }

    private static object ConvertClaimToNativeValue(Claim claim) =>
        claim.ValueType switch
        {
            ClaimValueTypes.String => claim.Value,
            ClaimValueTypes.Boolean when bool.TryParse(claim.Value, out var boolValue) => boolValue,
            ClaimValueTypes.Double when double.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue) => doubleValue,
            ClaimValueTypes.Integer or ClaimValueTypes.Integer32 when int.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue) => intValue,
            ClaimValueTypes.Integer64 when long.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var longValue) => longValue,
            ClaimValueTypes.DateTime when DateTimeOffset.TryParse(claim.Value, out var dateTimeOffsetValue) => dateTimeOffsetValue,
            ClaimValueTypes.DateTime when DateTime.TryParse(claim.Value, out var dateTimeValue) => dateTimeValue.ToUniversalTime(),
            JsonClaimValueTypes.Json => CreateJsonElement(claim.Value),
            JsonClaimValueTypes.JsonArray => CreateJsonElement(claim.Value),
            JsonClaimValueTypes.JsonNull => string.Empty,
            _ => claim.Value
        };

    private static JsonElement CreateJsonElement(string json)
    {
        var byteCount = Encoding.UTF8.GetByteCount(json);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: false, out Span<byte> buffer);
        Encoding.UTF8.GetBytes(json, buffer);

        var reader = new Utf8JsonReader(buffer);
        return JsonElement.TryParseValue(ref reader, out var jsonElement) ?
            jsonElement.Value :
            default;
    }
}
