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

/// <summary>
/// Provides the signature for a delegate that is used to return a collection of <see cref="SecretKey"/> instances
/// that are to be used to validate a Json Web Token (JWT).
/// </summary>
public delegate ValueTask<IEnumerable<SecretKey>> ResolveValidationKeysAsync(
    CompactJwt compactJwt,
    PropertyBag propertyBag,
    ISecretKeyProvider secretKeyProvider,
    IEnumerable<string> secretKeyTags,
    CancellationToken cancellationToken);

/// <summary>
/// Contains the signature for a delegate that is used to create a <see cref="ClaimsIdentity"/> instance
/// from a decoded Json Web Token (JWT).
/// </summary>
public delegate ValueTask<ClaimsIdentity> CreateClaimsIdentityAsync(
    DecodedJwt decodedJwt,
    PropertyBag propertyBag,
    string authenticationType,
    string nameClaimType,
    string roleClaimType,
    CancellationToken cancellationToken);

/// <summary>
/// Contains a set of parameters that are used to validate a Json Web Token (JWT).
/// </summary>
public class ValidateJwtParameters
{
    /// <summary>
    /// Gets a <see cref="PropertyBag"/> that can be used to store custom state information.
    /// This instance will be cloned for each JWT operation.
    /// </summary>
    public PropertyBag PropertyBag { get; } = new();

    /// <summary>
    /// Gets or sets a <see cref="string"/> collection of tags that are used to filter which <see cref="SecretKey"/>
    /// instances will be used when a specific <see cref="SecretKey"/> cannot be found. If this collection is empty,
    /// then all <see cref="SecretKey"/> instances will be used when a specific <see cref="SecretKey"/> cannot be found.
    /// The default value is an empty collection.
    /// </summary>
    public IEnumerable<string> SecretKeyTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the <c>AuthenticationType</c> that is used when creating <see cref="ClaimsIdentity"/> instances.
    /// </summary>
    public string AuthenticationType { get; set; } = "TODO";

    /// <summary>
    /// Gets or sets a <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Name</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.
    /// </summary>
    public string NameClaimType { get; set; } = ClaimsIdentity.DefaultNameClaimType;

    /// <summary>
    /// Gets or sets a <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Role</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.
    /// </summary>
    public string RoleClaimType { get; set; } = ClaimsIdentity.DefaultRoleClaimType;

    /// <summary>
    /// Gets a collection of <see cref="IValidateJwtHandler"/> instances that are used to validate the claims in a Json Web Token (JWT).
    /// </summary>
    public ICollection<IValidateJwtHandler> Handlers { get; } = new List<IValidateJwtHandler>();

    /// <summary>
    /// Gets or sets a delegate that is used to return a collection of <see cref="SecretKey"/> instances that are to be used
    /// to validate a Json Web Token (JWT).
    /// </summary>
    public ResolveValidationKeysAsync ResolveValidationKeysAsync { get; set; }

    /// <summary>
    /// Gets or sets a delegate that is used to create a <see cref="ClaimsIdentity"/> instance from a Json Web Token (JWT).
    /// </summary>
    public CreateClaimsIdentityAsync CreateClaimsIdentityAsync { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateJwtParameters"/> class.
    /// </summary>
    public ValidateJwtParameters()
    {
        ResolveValidationKeysAsync = (compactJwt, _, secretKeyProvider, secretKeyTags, _) =>
            ValueTask.FromResult(
                DefaultResolveValidationKeys(
                    compactJwt.DeserializedHeader,
                    secretKeyProvider.SecretKeys,
                    secretKeyTags));

        CreateClaimsIdentityAsync = (decodedJet, _, authenticationType, nameClaimType, roleClaimType, _) =>
            ValueTask.FromResult(
                DefaultCreateClaimsIdentity(
                    authenticationType,
                    nameClaimType,
                    roleClaimType,
                    decodedJet.Payload));
    }

    private static IEnumerable<SecretKey> DefaultResolveValidationKeys(
        JsonElement header,
        ISecretKeyCollection secretKeys,
        IEnumerable<string> secretKeyTags)
    {
        // attempt to lookup by 'kid'
        if (header.TryGetPropertyValue<string>(JwtClaimNames.Kid, out var keyId) &&
            secretKeys.TryGetByKeyId(keyId, out var specificKey))
        {
            return new[] { specificKey };
        }

        // attempt to lookup by certificate thumbprint
        var hasThumbprintSha1 = header.TryGetPropertyValue<string>(JwtClaimNames.X5t, out var thumbprintSha1);
        var hasThumbprintSha256 = header.TryGetPropertyValue<string>(JwtClaimNames.X5tS256, out var thumbprintSha256);

        if (hasThumbprintSha1 && secretKeys.TryGetByKeyId(thumbprintSha1!, out specificKey))
        {
            return new[] { specificKey };
        }

        if (hasThumbprintSha256 && secretKeys.TryGetByKeyId(thumbprintSha256!, out specificKey))
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

            foreach (var secretKey in secretKeys)
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
        return secretKeys.GetByTags(secretKeyTags);
    }

    private static bool VerifyCertificateHash(
        X509Certificate certificate,
        HashAlgorithmName hashAlgorithmName,
        ReadOnlySpan<byte> expected,
        Span<byte> actual)
    {
        if (expected.IsEmpty) return false;

        var result = certificate.TryGetCertHash(
            hashAlgorithmName,
            actual,
            out var bytesWritten);

        return result &&
               bytesWritten == expected.Length &&
               expected.SequenceEqual(actual[..bytesWritten]);
    }

    private static ClaimsIdentity DefaultCreateClaimsIdentity(
        string authenticationType,
        string nameClaimType,
        string roleClaimType,
        JsonElement payload)
    {
        var effectiveNameClaimType = string.IsNullOrEmpty(nameClaimType) ?
            ClaimsIdentity.DefaultNameClaimType :
            nameClaimType;

        var effectiveRoleClaimType = string.IsNullOrEmpty(roleClaimType) ?
            ClaimsIdentity.DefaultRoleClaimType :
            roleClaimType;

        var subject = new ClaimsIdentity(
            authenticationType,
            effectiveNameClaimType,
            effectiveRoleClaimType);

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

        return subject;
    }

    private static Claim CreateClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject) =>
        jsonElement.ValueKind switch
        {
            JsonValueKind.Undefined => throw new NotSupportedException(),
            JsonValueKind.Null => new Claim(propertyName, string.Empty, JsonClaimValueTypes.Null, issuer, issuer, subject),
            JsonValueKind.Object => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.Json, issuer, issuer, subject),
            JsonValueKind.Array => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.JsonArray, issuer, issuer, subject),
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

        try
        {
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
        }
        catch
        {
            // ignored
        }

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }

    private static Claim CreateNumberClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

        try
        {
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
        }
        catch
        {
            // ignored
        }

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }
}
