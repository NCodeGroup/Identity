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
using System.Text.Json;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretPublicKeyConverter"/>. Whether a secret exposes a
/// public key is decided by the deserialized key's runtime type against the <see cref="ISecretKeyTypeCatalog"/>
/// (the single source of truth, ADR-0067) — so a certificate secret, which resolves to an RSA or ECC key, is
/// handled uniformly. For an exportable key it projects the public half into a <c>JWK</c> (reusing the registered
/// <see cref="IJsonWebKeyConverter"/> pipeline, the same source of truth the JWKS endpoint uses) and a
/// <c>SubjectPublicKeyInfo</c> <c>PEM</c>. Secrets without a public key yield <c>null</c>.
/// </summary>
internal class DefaultSecretPublicKeyConverter(
    ISecretSerializer secretSerializer,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IEnumerable<IJsonWebKeyConverter> jsonWebKeyConverters
) : ISecretPublicKeyConverter
{
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private ImmutableArray<IJsonWebKeyConverter> JsonWebKeyConverters { get; } =
    [.. jsonWebKeyConverters];

    /// <inheritdoc />
    public PublicKeyResource? Convert(PersistedSecret secret)
    {
        var secretKey = SecretSerializer.DeserializeSecret(secret);

        // Export capability is keyed off the runtime key type (a certificate resolves to an RSA or ECC key).
        if (
            secretKey is not AsymmetricSecretKey asymmetricSecretKey
            || !SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor)
            || !descriptor.SupportsPublicKeyExport
        )
        {
            return null;
        }

        var jsonWebKey =
            ConvertToJsonWebKey(secretKey)
            ?? throw new InvalidOperationException(
                $"No JSON Web Key converter is registered for the '{secretKey.KeyType}' key type."
            );

        var jwk = JsonSerializer.SerializeToElement(jsonWebKey, JsonSerializerOptions.Web);
        var pem = asymmetricSecretKey.ExportSubjectPublicKeyInfoPem();

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
}
