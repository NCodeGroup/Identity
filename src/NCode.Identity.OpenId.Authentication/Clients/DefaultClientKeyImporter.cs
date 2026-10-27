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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Json;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IClientKeyImporter"/>. A key's type is matched to its registered
/// <see cref="ISecretKeyTypeDescriptor"/> by its JSON Web Key <c>kty</c>, its material is parsed by that descriptor
/// (which refuses private material), and its declared <c>alg</c> is checked against the registered algorithms, so a key
/// type or algorithm a package registers is accepted without changes here.
/// </summary>
// See ADR-0161.
internal class DefaultClientKeyImporter(
    ILogger<DefaultClientKeyImporter> logger,
    ICryptoService cryptoService,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    ISecretImporter secretImporter,
    IAlgorithmCollectionProvider algorithmCollectionProvider
) : IClientKeyImporter
{
    // RFC 7517 §4.3: the key operations that belong to each public key use. An operation outside both is not judged.
    private static FrozenSet<string> SignatureOperations { get; } =
        new[] { "sign", "verify" }.ToFrozenSet(StringComparer.Ordinal);

    private static FrozenSet<string> EncryptionOperations { get; } =
        new[]
        {
            "encrypt",
            "decrypt",
            "wrapKey",
            "unwrapKey",
            "deriveKey",
            "deriveBits",
        }.ToFrozenSet(StringComparer.Ordinal);

    private ILogger<DefaultClientKeyImporter> Logger { get; } = logger;
    private ICryptoService CryptoService { get; } = cryptoService;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private ISecretImporter SecretImporter { get; } = secretImporter;
    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    /// <inheritdoc />
    public bool TryImportRegisteredKeySet(
        JsonElement keySet,
        DateTimeOffset createdWhen,
        [NotNullWhen(true)] out IReadOnlyList<PersistedSecret>? secrets,
        [NotNullWhen(false)] out string? error
    )
    {
        secrets = null;
        if (!TryGetKeys(keySet, out var keys, out error))
            return false;

        var imported = new List<PersistedSecret>();
        var keyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in keys.EnumerateArray())
        {
            if (!TryGetKey(element, out var key))
            {
                error =
                    "Every key in the JSON Web Key Set must be a JSON object with a 'kty' member.";
                return false;
            }

            if (!TryImportSetKey(key, createdWhen, out var secret, out error))
                return false;

            if (!keyIds.Add(key.KeyId ?? string.Empty))
            {
                error = DuplicateKeyIdError(key);
                return false;
            }

            imported.Add(secret);
        }

        secrets = imported;
        return true;
    }

    /// <inheritdoc />
    public bool TryImportPublishedKeySet(
        JsonElement keySet,
        DateTimeOffset createdWhen,
        [NotNullWhen(true)] out IReadOnlyList<PersistedSecret>? secrets,
        [NotNullWhen(false)] out string? error
    )
    {
        secrets = null;
        if (!TryGetKeys(keySet, out var keys, out error))
            return false;

        var imported = new List<PersistedSecret>();
        var keyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in keys.EnumerateArray())
        {
            // RFC 7517 §5: a member that is not a key is ignored.
            if (!TryGetKey(element, out var key))
                continue;

            // A published private key is compromised, and a set that publishes one is not used at all.
            if (key.ContainsPrivateKeyMaterial)
            {
                error = PrivateKeyMaterialError(key);
                return false;
            }

            if (!TryImportSetKey(key, createdWhen, out var secret, out var keyError))
            {
                Logger.ClientJwksKeyIgnored(key.KeyType, key.KeyId, keyError);
                continue;
            }

            if (!keyIds.Add(key.KeyId ?? string.Empty))
            {
                error = DuplicateKeyIdError(key);
                return false;
            }

            imported.Add(secret);
        }

        secrets = imported;
        return true;
    }

    /// <inheritdoc />
    public bool TryImportKey(
        SecretKeyImportRequest request,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    )
    {
        secret = null;
        if (!SecretKeyTypeCatalog.TryGet(request.SecretType, out var descriptor))
        {
            error = $"The secret type '{request.SecretType}' is not supported.";
            return false;
        }

        if (request.Format != SecretKeyImportFormats.Jwk)
            return TryImport(request, descriptor, operationsUse: null, out secret, out error);

        if (!TryParseKey(request.KeyMaterial, out var key))
        {
            error = "The key material is not a JSON Web Key: a JSON object with a 'kty' member.";
            return false;
        }

        if (key.ContainsPrivateKeyMaterial)
        {
            error = PrivateKeyMaterialError(key);
            return false;
        }

        if (
            !TryValidateKeyMembers(key, out var operationsUse, out error)
            || !TryMerge(
                JsonWebKeyParameterNames.KeyId,
                request.KeyId,
                key,
                out var keyId,
                out error
            )
            || !TryMerge(
                JsonWebKeyParameterNames.PublicKeyUse,
                request.Use,
                key,
                out var use,
                out error
            )
            || !TryMerge(
                JsonWebKeyParameterNames.Algorithm,
                request.Algorithm,
                key,
                out var algorithm,
                out error
            )
        )
        {
            return false;
        }

        var merged = new SecretKeyImportRequest
        {
            SecretId = request.SecretId,
            KeyId = keyId,
            SecretType = request.SecretType,
            Format = request.Format,
            KeyMaterial = request.KeyMaterial,
            Use = use,
            Algorithm = algorithm,
            CreatedWhen = request.CreatedWhen,
            ExpiresWhen = request.ExpiresWhen,
        };

        return TryImport(merged, descriptor, operationsUse, out secret, out error);
    }

    private static bool TryGetKeys(
        JsonElement keySet,
        out JsonElement keys,
        [NotNullWhen(false)] out string? error
    )
    {
        // RFC 7517 §5: a JSON Web Key Set is an object whose 'keys' member is an array of keys.
        if (
            keySet.ValueKind == JsonValueKind.Object
            && keySet.TryGetProperty("keys", out keys)
            && keys.ValueKind == JsonValueKind.Array
        )
        {
            error = null;
            return true;
        }

        keys = default;
        error = "The JSON Web Key Set must be a JSON object with a 'keys' array.";
        return false;
    }

    private bool TryGetKey(JsonElement element, [NotNullWhen(true)] out JsonWebKey? key)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetPropertyValue<string>(JsonWebKeyParameterNames.KeyType, out var kty)
            && kty.Length > 0
        )
        {
            key = new JsonWebKey(element);

            // A key of a registered type carries that type's JWK rules, so its own private members are detected.
            if (
                SecretKeyTypeCatalog.TryGetByJwkKeyType(key.KeyType, out var descriptor)
                && descriptor.JwkKeyType is { } jwkKeyType
            )
            {
                key = new JsonWebKey(element, jwkKeyType);
            }

            return true;
        }

        key = null;
        return false;
    }

    private bool TryParseKey(string keyMaterial, [NotNullWhen(true)] out JsonWebKey? key)
    {
        JsonElement element;
        try
        {
            element = JsonElement.Parse(keyMaterial);
        }
        catch (JsonException)
        {
            key = null;
            return false;
        }

        return TryGetKey(element, out key);
    }

    /// <summary>
    /// Imports one key of a JSON Web Key Set: a key of a registered type that carries a <c>kid</c> and consistent
    /// metadata, identified by that <c>kid</c> under a newly generated secret identifier.
    /// </summary>
    private bool TryImportSetKey(
        JsonWebKey key,
        DateTimeOffset createdWhen,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    )
    {
        secret = null;

        if (key.ContainsPrivateKeyMaterial)
        {
            error = PrivateKeyMaterialError(key);
            return false;
        }

        if (!SecretKeyTypeCatalog.TryGetByJwkKeyType(key.KeyType, out var descriptor))
        {
            error = $"The key type '{key.KeyType}' is not supported.";
            return false;
        }

        // Keys are selected by 'kid', which the client names its keys by; the server never invents one for it.
        if (string.IsNullOrEmpty(key.KeyId))
        {
            error = "Every key in the JSON Web Key Set must have a 'kid'.";
            return false;
        }

        if (!TryValidateKeyMembers(key, out var operationsUse, out error))
            return false;

        var request = new SecretKeyImportRequest
        {
            SecretId = CryptoService.GenerateResourceId(),
            KeyId = key.KeyId,
            SecretType = descriptor.KeyType,
            Format = SecretKeyImportFormats.Jwk,
            KeyMaterial = key.ToJsonString(),
            Use = key.PublicKeyUse,
            Algorithm = key.Algorithm,
            CreatedWhen = createdWhen,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

        return TryImport(request, descriptor, operationsUse, out secret, out error);
    }

    /// <summary>
    /// Validates the metadata members a key declares about itself: <c>kid</c>, <c>use</c>, and <c>alg</c> are strings,
    /// <c>use</c> is <c>sig</c> or <c>enc</c>, and <c>key_ops</c> is an array of distinct strings whose known operations
    /// all belong to one use, which agrees with <c>use</c> when both are present (RFC 7517 §4.3).
    /// </summary>
    private static bool TryValidateKeyMembers(
        JsonWebKey key,
        out string? operationsUse,
        [NotNullWhen(false)] out string? error
    )
    {
        operationsUse = null;
        var element = key.AsElement();

        string[] stringMembers =
        [
            JsonWebKeyParameterNames.KeyId,
            JsonWebKeyParameterNames.PublicKeyUse,
            JsonWebKeyParameterNames.Algorithm,
        ];
        foreach (var name in stringMembers)
        {
            if (
                element.TryGetProperty(name, out var member)
                && member.ValueKind != JsonValueKind.String
            )
            {
                error = $"The key's '{name}' member must be a string.";
                return false;
            }
        }

        var use = key.PublicKeyUse;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            error =
                $"The key's 'use' value '{use}' is not supported; expected '{SecretKeyUses.Signature}' or "
                + $"'{SecretKeyUses.Encryption}'.";
            return false;
        }

        if (!element.TryGetProperty(JsonWebKeyParameterNames.KeyOperations, out var keyOperations))
        {
            error = null;
            return true;
        }

        if (
            keyOperations.ValueKind != JsonValueKind.Array
            || keyOperations
                .EnumerateArray()
                .Any(operation => operation.ValueKind != JsonValueKind.String)
        )
        {
            error = "The key's 'key_ops' member must be an array of strings.";
            return false;
        }

        var operations = key.KeyOperations ?? [];
        if (operations.Distinct(StringComparer.Ordinal).Count() != operations.Count)
        {
            error = "The key's 'key_ops' member must not repeat an operation.";
            return false;
        }

        var usesSignature = operations.Any(SignatureOperations.Contains);
        var usesEncryption = operations.Any(EncryptionOperations.Contains);
        if (usesSignature && usesEncryption)
        {
            error =
                "The key's 'key_ops' member must not combine signature and encryption operations.";
            return false;
        }

        operationsUse =
            usesSignature ? SecretKeyUses.Signature
            : usesEncryption ? SecretKeyUses.Encryption
            : null;

        if (use is not null && operationsUse is not null && use != operationsUse)
        {
            error = $"The key's 'key_ops' member is inconsistent with its 'use' value '{use}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryMerge(
        string memberName,
        string? requested,
        JsonWebKey key,
        out string? value,
        [NotNullWhen(false)] out string? error
    )
    {
        var declared = key.GetMember(memberName);
        if (
            requested is not null
            && declared is not null
            && !string.Equals(requested, declared, StringComparison.Ordinal)
        )
        {
            value = null;
            error =
                $"The requested '{memberName}' value '{requested}' contradicts the key's declared value '{declared}'.";
            return false;
        }

        value = requested ?? declared;
        error = null;
        return true;
    }

    /// <summary>
    /// Imports a public key through its descriptor and checks that the algorithm it declares, if any, agrees
    /// with its use and accepts the key's type and size.
    /// </summary>
    private bool TryImport(
        SecretKeyImportRequest request,
        ISecretKeyTypeDescriptor descriptor,
        string? operationsUse,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    )
    {
        secret = null;

        if (descriptor.PublicKeyImportFormats.Count == 0)
        {
            error =
                $"A '{descriptor.KeyType}' key is secret key material and cannot be imported as a public key.";
            return false;
        }

        if (!descriptor.PublicKeyImportFormats.Contains(request.Format))
        {
            error =
                $"The import format '{request.Format}' is not supported for a '{descriptor.KeyType}' key; expected "
                + string.Join(
                    ", ",
                    descriptor.PublicKeyImportFormats.Select(format => $"'{format}'")
                )
                + ".";
            return false;
        }

        if (request.KeyId is { Length: > SecretMaxLengths.KeyId })
        {
            error = $"The key's 'kid' must not exceed {SecretMaxLengths.KeyId} characters.";
            return false;
        }

        var use = request.Use;
        if (use is not null && use is not (SecretKeyUses.Signature or SecretKeyUses.Encryption))
        {
            error =
                $"The use '{use}' is not supported; expected '{SecretKeyUses.Signature}' or "
                + $"'{SecretKeyUses.Encryption}'.";
            return false;
        }

        if (use is not null && operationsUse is not null && use != operationsUse)
        {
            error = $"The key's 'key_ops' member is inconsistent with the use '{use}'.";
            return false;
        }

        PersistedSecret imported;
        try
        {
            imported = SecretImporter.ImportSecret(request);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or CryptographicException)
        {
            error = $"The key is invalid: {exception.Message}";
            return false;
        }

        if (request.Algorithm is { } algorithm)
        {
            error = AlgorithmCollectionProvider.Collection.ValidateKeyAlgorithm(
                new KeyAlgorithmDeclaration
                {
                    KeyType = descriptor.KeyClrType,
                    KeySizeBits = imported.KeySizeBits,
                    Use = use ?? operationsUse,
                    Algorithm = algorithm,
                }
            );
            if (error is not null)
                return false;
        }

        secret = imported;
        error = null;
        return true;
    }

    private static string PrivateKeyMaterialError(JsonWebKey key) =>
        $"The key '{key.KeyId ?? key.KeyType}' carries private or symmetric key material; only public keys may be "
        + "registered.";

    private static string DuplicateKeyIdError(JsonWebKey key) =>
        $"The 'kid' value '{key.KeyId}' is not unique within the JSON Web Key Set.";
}
