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
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="ISecretPublicKeyConverter"/>. It reads the secret's stored
/// public half through <see cref="ISecretSerializer.DeserializePublicSecret"/> and never unprotects private
/// material, then asks the key's <see cref="ISecretKeyTypeDescriptor"/> (the single extensibility seam) to export
/// it as a <c>JWK</c> and a <c>SubjectPublicKeyInfo</c> <c>PEM</c>. Secrets without exportable public material (for
/// example symmetric keys) yield <c>null</c>.
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
    // See ADR-0268.
    public PublicKeyResource? Convert(PersistedSecret secret)
    {
        if (
            !SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor)
            || !descriptor.SupportsPublicKeyExport
            || !secret.HasPublicKey()
        )
        {
            return null;
        }

        var secretKey = SecretSerializer.DeserializePublicSecret(secret);
        var exported = descriptor.ExportPublicKey(secretKey);
        return BuildResource(secret, exported);
    }

    private static PublicKeyResource? BuildResource(
        PersistedSecret secret,
        ExportedPublicKey? exported
    )
    {
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
