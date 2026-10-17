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

using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretPublicKeyConverter"/>. It deserializes the secret and
/// asks the key's <see cref="ISecretKeyTypeDescriptor"/> (the single extensibility seam, ADR-0104) to export its
/// public material as a <c>JWK</c> and a <c>SubjectPublicKeyInfo</c> <c>PEM</c>. Secrets without exportable public
/// material (for example symmetric keys) yield <c>null</c>.
/// </summary>
// See ADR-0104.
internal class DefaultSecretPublicKeyConverter(
    ISecretSerializer secretSerializer,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ISecretPublicKeyConverter
{
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public PublicKeyResource? Convert(PersistedSecret secret)
    {
        var secretKey = SecretSerializer.DeserializeSecret(secret);

        if (!SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor))
        {
            return null;
        }

        var exported = descriptor.ExportPublicKey(secretKey);
        if (exported is null)
        {
            return null;
        }

        return new PublicKeyResource
        {
            SecretId = secret.SecretId,
            SecretType = secret.SecretType,
            Jwk = exported.Jwk.AsElement(),
            Pem = exported.Pem,
        };
    }
}
