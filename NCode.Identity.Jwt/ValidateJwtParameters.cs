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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose;
using NCode.Jose.Json;

namespace NCode.Identity.Jwt;

public delegate ValueTask<ISecretKeyCollection> ResolveProviderKeysAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    ISecretKeyProvider secretKeyProvider,
    CancellationToken cancellationToken);

public delegate ValueTask<IEnumerable<string>> ResolveSecretKeyTagsAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    CancellationToken cancellationToken);

public delegate ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    ISecretKeyCollection candidateKeys,
    IEnumerable<string> secretKeyTags,
    CancellationToken cancellationToken);

public delegate ValueTask<ClaimsIdentity> CreateSubjectAsync(
    DecodedJwt decodedJwt,
    PropertyBag propertyBag,
    CancellationToken cancellationToken);

public delegate ValueTask AddClaimsToSubjectAsync(
    ClaimsIdentity subject,
    DecodedJwt decodedJwt,
    PropertyBag propertyBag,
    CancellationToken cancellationToken);

public class ValidateJwtParameters
{
    /// <summary>
    /// Gets a <see cref="PropertyBag"/> that can be used to store custom state information.
    /// This instance will be cloned for each JWT operation.
    /// </summary>
    public PropertyBag PropertyBag { get; } = new();

    public IEnumerable<string> SecretKeyTags { get; set; } = Array.Empty<string>();

    public string AuthenticationType { get; set; } = "TODO";

    public string NameClaimType { get; set; } = ClaimsIdentity.DefaultNameClaimType;

    public string RoleClaimType { get; set; } = ClaimsIdentity.DefaultRoleClaimType;

    public ICollection<IValidateJwtHandler> Handlers { get; } = new List<IValidateJwtHandler>();

    public ValidateJwtParameters()
    {
        ResolveProviderKeysAsync = (_, _, secretKeyProvider, _) => ValueTask.FromResult(secretKeyProvider.SecretKeys);

        ResolveSecretKeyTagsAsync = (_, _, _) => ValueTask.FromResult(SecretKeyTags);

        ResolveValidationKeysAsync = (compactJwt, _, candidateKeys, secretKeyTags, _) => ValueTask.FromResult(
            DefaultResolveValidationKeys(
                compactJwt,
                candidateKeys,
                secretKeyTags));

        CreateSubjectAsync = (_, _, _) => ValueTask.FromResult(DefaultCreateSubject(
            AuthenticationType,
            NameClaimType,
            RoleClaimType));

        AddClaimsToSubjectAsync = (subject, decodedJet, _, _) => DefaultClaimsToSubjectAsync(subject, decodedJet);
    }

    public ResolveProviderKeysAsync ResolveProviderKeysAsync { get; set; }
    public ResolveSecretKeyTagsAsync ResolveSecretKeyTagsAsync { get; set; }
    public ResolveValidationKeysAsync ResolveValidationKeysAsync { get; set; }
    public CreateSubjectAsync CreateSubjectAsync { get; set; }
    public AddClaimsToSubjectAsync AddClaimsToSubjectAsync { get; set; }

    private static IEnumerable<SecretKey> DefaultResolveValidationKeys(
        CompactJwt compactJwt,
        ISecretKeyCollection candidateKeys,
        IEnumerable<string> secretKeyTags)
    {
        var header = compactJwt.DeserializedHeader;

        // attempt to lookup by 'kid'
        if (header.TryGetPropertyValue<string>(JwtClaimNames.Kid, out var keyId) &&
            candidateKeys.TryGetByKeyId(keyId, out var specificKey))
        {
            return new[] { specificKey };
        }

        // attempt to lookup by certificate thumbprint
        var hasThumbprintSha1 = header.TryGetPropertyValue<string>(JwtClaimNames.X5t, out var thumbprintSha1);
        var hasThumbprintSha256 = header.TryGetPropertyValue<string>(JwtClaimNames.X5tS256, out var thumbprintSha256);

        if (hasThumbprintSha1 && candidateKeys.TryGetByKeyId(thumbprintSha1!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha256 && candidateKeys.TryGetByKeyId(thumbprintSha256!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha1 || hasThumbprintSha256)
        {
            const int sha1HashSize = 20;
            const int sha256HashSize = 32;

            var expectedSha1 = hasThumbprintSha1 ? stackalloc byte[sha1HashSize] : default;
            if (hasThumbprintSha1)
            {
                var result = Base64Url.TryDecode(thumbprintSha1, expectedSha1, out var bytesWritten);
                Debug.Assert(result && bytesWritten == sha1HashSize);
            }

            var expectedSha256 = hasThumbprintSha256 ? stackalloc byte[sha256HashSize] : default;
            if (hasThumbprintSha256)
            {
                var result = Base64Url.TryDecode(thumbprintSha256, expectedSha256, out var bytesWritten);
                Debug.Assert(result && bytesWritten == sha256HashSize);
            }

            Span<byte> actualHash = stackalloc byte[sha256HashSize];

            foreach (var secretKey in candidateKeys)
            {
                if (secretKey is not AsymmetricSecretKey { Certificate: not null } asymmetricSecretKey) continue;
                var certificate = asymmetricSecretKey.Certificate;

                if (VerifyCertificateHash(certificate, HashAlgorithmName.SHA1, expectedSha1, actualHash))
                {
                    return new[] { secretKey };
                }

                if (VerifyCertificateHash(certificate, HashAlgorithmName.SHA256, expectedSha256, actualHash))
                {
                    return new[] { secretKey };
                }
            }
        }

        // otherwise, the degenerate case will attempt to use the keys with the specified tags
        return candidateKeys.GetByTags(secretKeyTags);
    }

    private static bool VerifyCertificateHash(
        X509Certificate certificate,
        HashAlgorithmName hashAlgorithmName,
        ReadOnlySpan<byte> expected,
        Span<byte> actual)
    {
        if (expected.IsEmpty) return false;

        var result = certificate.TryGetCertHash(hashAlgorithmName, actual, out var bytesWritten);
        Debug.Assert(result);

        return expected.SequenceEqual(actual[..bytesWritten]);
    }

    private static ClaimsIdentity DefaultCreateSubject(
        string authenticationType,
        string nameClaimType,
        string roleClaimType)
    {
        var effectiveNameClaimType = string.IsNullOrEmpty(nameClaimType) ?
            ClaimsIdentity.DefaultNameClaimType :
            nameClaimType;

        var effectiveRoleClaimType = string.IsNullOrEmpty(roleClaimType) ?
            ClaimsIdentity.DefaultRoleClaimType :
            roleClaimType;

        return new ClaimsIdentity(
            authenticationType,
            effectiveNameClaimType,
            effectiveRoleClaimType);
    }

    private static ValueTask DefaultClaimsToSubjectAsync(
        ClaimsIdentity subject,
        DecodedJwt decodedJwt)
    {
        var payload = decodedJwt.Payload;

        if (!payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Iss, out var issuer) || string.IsNullOrEmpty(issuer))
        {
            issuer = ClaimsIdentity.DefaultIssuer;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            if (value.ValueKind == JsonValueKind.Array)
            {
                subject.AddClaims(
                    value.EnumerateArray().Select(item =>
                        CreateClaim(name, item, issuer, subject)));
            }
            else
            {
                subject.AddClaim(CreateClaim(name, value, issuer, subject));
            }
        }

        return ValueTask.CompletedTask;
    }

    private static Claim CreateClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject) =>
        jsonElement.ValueKind switch
        {
            JsonValueKind.Undefined => throw new NotSupportedException(),
            JsonValueKind.Null => new Claim(propertyName, string.Empty, Jose.Jwt.JsonClaimValueTypes.Null, issuer, issuer, subject),
            JsonValueKind.Object => new Claim(propertyName, jsonElement.ToString(), Jose.Jwt.JsonClaimValueTypes.Json, issuer, issuer, subject),
            JsonValueKind.Array => new Claim(propertyName, jsonElement.ToString(), Jose.Jwt.JsonClaimValueTypes.JsonArray, issuer, issuer, subject),
            JsonValueKind.String => CreateStringClaim(propertyName, jsonElement, issuer, subject),
            JsonValueKind.Number => CreateNumberClaim(propertyName, jsonElement, issuer, subject),
            JsonValueKind.True => new Claim(propertyName, "true", ClaimValueTypes.Boolean, issuer, issuer, subject),
            JsonValueKind.False => new Claim(propertyName, "false", ClaimValueTypes.Boolean, issuer, issuer, subject),
            _ => throw new ArgumentOutOfRangeException(nameof(jsonElement), "Unsupported JsonValueKind.")
        };

    private static Claim CreateStringClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

        if (DateTime.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out var dateTimeValue))
        {
            stringValue = dateTimeValue.ToString("O", CultureInfo.InvariantCulture);
            valueType = ClaimValueTypes.DateTime;
        }

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }

    private static Claim CreateNumberClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

        if (jsonElement.TryGetInt16(out _))
            valueType = ClaimValueTypes.Integer;

        if (jsonElement.TryGetInt32(out _))
            valueType = ClaimValueTypes.Integer32;

        if (jsonElement.TryGetInt64(out _))
            valueType = ClaimValueTypes.Integer64;

        if (jsonElement.TryGetUInt32(out _))
            valueType = ClaimValueTypes.UInteger32;

        if (jsonElement.TryGetUInt64(out _))
            valueType = ClaimValueTypes.UInteger64;

        // no need to check single or decimal since the range of double is larger
        if (jsonElement.TryGetDouble(out _))
            valueType = ClaimValueTypes.Double;

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }
}
