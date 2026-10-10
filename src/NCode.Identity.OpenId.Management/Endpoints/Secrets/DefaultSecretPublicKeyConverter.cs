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

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretPublicKeyConverter"/>. It deserializes the persisted
/// secret and, for an asymmetric key, projects its public half into a <c>JWK</c> (reusing the registered
/// <see cref="IJsonWebKeyConverter"/> pipeline, the same source of truth the JWKS endpoint uses) and a
/// <c>SubjectPublicKeyInfo</c> <c>PEM</c>. Symmetric secrets have no public key and yield <c>null</c>.
/// </summary>
internal class DefaultSecretPublicKeyConverter(
    ISecretSerializer secretSerializer,
    IEnumerable<IJsonWebKeyConverter> jsonWebKeyConverters
) : ISecretPublicKeyConverter
{
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private ImmutableArray<IJsonWebKeyConverter> JsonWebKeyConverters { get; } =
    [.. jsonWebKeyConverters];

    /// <inheritdoc />
    public PublicKeyResource? Convert(PersistedSecret secret)
    {
        var secretKey = SecretSerializer.DeserializeSecret(secret);
        if (secretKey is not AsymmetricSecretKey asymmetricSecretKey)
        {
            // A symmetric secret has no public key material to expose.
            return null;
        }

        var jsonWebKey =
            ConvertToJsonWebKey(secretKey)
            ?? throw new InvalidOperationException(
                $"No JSON Web Key converter is registered for the '{secret.SecretType}' secret type."
            );

        var jwk = JsonSerializer.SerializeToElement(jsonWebKey, JsonSerializerOptions.Web);
        var pem = ExportSubjectPublicKeyInfoPem(asymmetricSecretKey);

        return new PublicKeyResource
        {
            SecretId = secret.SecretId,
            SecretType = secret.SecretType,
            Jwk = jwk,
            Pem = pem,
        };
    }

    private JsonWebKey? ConvertToJsonWebKey(SecretKey secretKey)
    {
        // The first registered converter that handles the key wins, matching the JWKS endpoint's resolution.
        foreach (var converter in JsonWebKeyConverters)
        {
            if (converter.TryConvert(secretKey, out var jsonWebKey))
            {
                return jsonWebKey;
            }
        }

        return null;
    }

    private static string ExportSubjectPublicKeyInfoPem(AsymmetricSecretKey asymmetricSecretKey)
    {
        switch (asymmetricSecretKey)
        {
            case RsaSecretKey rsaSecretKey:
            {
                using var rsa = rsaSecretKey.ExportRSA();
                return rsa.ExportSubjectPublicKeyInfoPem();
            }
            case EccSecretKey eccSecretKey:
            {
                using var ecdsa = eccSecretKey.ExportECDsa();
                return ecdsa.ExportSubjectPublicKeyInfoPem();
            }
            default:
                throw new InvalidOperationException(
                    $"Cannot export public key material for the '{asymmetricSecretKey.KeyType}' secret key type."
                );
        }
    }
}
