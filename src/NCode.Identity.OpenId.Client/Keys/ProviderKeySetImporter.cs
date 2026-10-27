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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Client.Keys;

/// <summary>
/// Imports the public signature verification keys of a provider's JSON Web Key Set through the registered secret key
/// types. A set that carries private or symmetric key material is refused as a whole, because a provider must publish
/// public keys only; a key that is merely not usable for verification is skipped.
/// </summary>
internal class ProviderKeySetImporter(ISecretKeyTypeCatalog secretKeyTypeCatalog)
{
    private const string SigningUse = "sig";
    private const string VerifyOperation = "verify";
    private const string SymmetricKeyType = "oct";

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <summary>
    /// Imports the verification keys of the specified JSON Web Key Set.
    /// </summary>
    public bool TryImport(
        JsonElement keySet,
        [NotNullWhen(true)] out List<SecretKey>? keys,
        [NotNullWhen(false)] out string? error
    )
    {
        keys = null;

        if (
            keySet.ValueKind != JsonValueKind.Object
            || !keySet.TryGetProperty("keys", out var keysElement)
            || keysElement.ValueKind != JsonValueKind.Array
        )
        {
            error = "The JSON Web Key Set is not an object with a 'keys' array.";
            return false;
        }

        var imported = new List<SecretKey>();
        var keyIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in keysElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            JsonWebKey jwk;
            ISecretKeyTypeDescriptor? descriptor;
            try
            {
                jwk = new JsonWebKey(element);
                SecretKeyTypeCatalog.TryGetByJwkKeyType(jwk.KeyType, out descriptor);
                if (descriptor?.JwkKeyType is { } jwkKeyType)
                    jwk = new JsonWebKey(element, jwkKeyType);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (
                jwk.ContainsPrivateKeyMaterial
                || string.Equals(jwk.KeyType, SymmetricKeyType, StringComparison.Ordinal)
            )
            {
                error = "The JSON Web Key Set must not contain private or symmetric key material.";
                return false;
            }

            if (
                jwk.PublicKeyUse is { } use
                && !string.Equals(use, SigningUse, StringComparison.Ordinal)
            )
                continue;

            if (
                jwk.KeyOperations is { } operations
                && !operations.Contains(VerifyOperation, StringComparer.Ordinal)
            )
                continue;

            if (
                descriptor is null
                || !descriptor.IsAsymmetric
                || !descriptor.PublicKeyImportFormats.Contains(SecretKeyImportFormats.Jwk)
            )
                continue;

            SecretKey secretKey;
            try
            {
                byte[] keyMaterial = [];
                var importedMaterial = descriptor.ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Jwk,
                        KeyMaterial = element.GetRawText(),
                    },
                    bytes =>
                    {
                        keyMaterial = bytes.ToArray();
                        return string.Empty;
                    }
                );
                secretKey = descriptor.DeserializeKeyMaterial(
                    new KeyMetadata
                    {
                        KeyId = jwk.KeyId,
                        Use = jwk.PublicKeyUse,
                        Algorithm = jwk.Algorithm,
                    },
                    importedMaterial.SecretEncoding,
                    keyMaterial
                );
            }
            catch (Exception exception)
                when (exception
                        is ArgumentException
                            or FormatException
                            or JsonException
                            or NotSupportedException
                            or CryptographicException
                )
            {
                continue;
            }

            if (jwk.KeyId is { } keyId && !keyIds.Add(keyId))
            {
                error = "The JSON Web Key Set contains more than one key with the same 'kid'.";
                return false;
            }

            imported.Add(secretKey);
        }

        if (imported.Count == 0)
        {
            error = "The JSON Web Key Set contains no usable signature verification key.";
            return false;
        }

        keys = imported;
        error = null;
        return true;
    }
}
