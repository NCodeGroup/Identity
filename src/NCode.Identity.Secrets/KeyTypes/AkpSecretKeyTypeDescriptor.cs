// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.Secrets.KeyTypes;

// See the note on AkpSecretKey: ML-DSA PKCS#8 / SPKI DER is [Experimental] (SYSLIB5006) but spec-fixed (RFC 9881). See ADR-1118.
#pragma warning disable SYSLIB5006

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <c>AKP</c> (Algorithm Key Pair) keys backing post-quantum
/// <c>ML-DSA</c> (FIPS 204 / RFC 9964) signatures. Generates key pairs, imports public <c>JWK</c>
/// (<c>kty</c>=<c>AKP</c>) / <c>PEM</c> material, and deserializes stored <c>PKCS#8</c> / <c>SPKI</c> DER into
/// <see cref="AkpSecretKey"/> instances, data-protecting the private key in memory.
/// </summary>
/// <param name="dataProtectorFactory">
/// The <see cref="IDataProtectorFactory{T}"/> used to protect and unprotect private keys at rest in memory.
/// </param>
// See ADR-1118.
[PublicAPI]
public sealed class AkpSecretKeyTypeDescriptor(
    IDataProtectorFactory<SecretKey> dataProtectorFactory
) : ISecretKeyTypeDescriptor
{
    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    /// <inheritdoc />
    public string KeyType => AkpSecretKey.TypeName;

    /// <inheritdoc />
    public Type KeyClrType => typeof(AkpSecretKey);

    /// <inheritdoc />
    public JsonWebKeyTypeDescriptor JwkKeyType => JsonWebKeyTypeDescriptors.Akp;

    /// <inheritdoc />
    public bool IsAsymmetric => true;

    /// <inheritdoc />
    public bool SupportsPublicKeyExport => true;

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublicKeyImportFormats { get; } =
    [SecretKeyImportFormats.Jwk, SecretKeyImportFormats.Pem];

    /// <inheritdoc />
    public string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        TryResolveAlgorithm(parameters, out _, out var error) ? null : error;

    /// <inheritdoc />
    public GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        if (!TryResolveAlgorithm(parameters, out var algorithm, out var error))
        {
            throw new ArgumentException(error, nameof(parameters));
        }

        using var key = MLDsa.GenerateKey(algorithm);
        var pkcs8 = key.ExportPkcs8PrivateKey();
        try
        {
            return new GeneratedSecretKeyMaterial
            {
                ProtectedKeyMaterial = protect(pkcs8),
                KeySizeBits = algorithm.PublicKeySizeInBytes * 8,
                SecretEncoding = SecretEncodings.Pkcs8,
                SecretMaterial = null,
                PublicKey = ExportBase64UrlSubjectPublicKeyInfo(key),
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    /// <summary>
    /// Exports the <c>SubjectPublicKeyInfo</c> of the specified key into a rented buffer, growing the buffer until the
    /// export succeeds, and returns it base64url-encoded.
    /// </summary>
    private static string ExportBase64UrlSubjectPublicKeyInfo(MLDsa key)
    {
        var byteCount = 4096;
        while (true)
        {
            using var _ = BufferFactory.Rent(byteCount, isSensitive: false, out Span<byte> span);
            if (key.TryExportSubjectPublicKeyInfo(span, out var bytesWritten))
            {
                return Base64Url.EncodeToString(span[..bytesWritten]);
            }

            byteCount = checked(byteCount * 2);
        }
    }

    /// <inheritdoc />
    public ImportedSecretKeyMaterial ImportPublicKeyMaterial(
        SecretKeyImportParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        var subjectPublicKeyInfo = parameters.Format switch
        {
            SecretKeyImportFormats.Jwk => ParseJwkPublicKey(parameters.KeyMaterial),
            SecretKeyImportFormats.Pem => ParsePemPublicKey(parameters.KeyMaterial),
            _ => throw new ArgumentException(
                $"The import format '{parameters.Format}' is not supported for AKP keys.",
                nameof(parameters)
            ),
        };

        using var key = MLDsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo);
        return new ImportedSecretKeyMaterial
        {
            ProtectedKeyMaterial = protect(subjectPublicKeyInfo),
            KeySizeBits = key.Algorithm.PublicKeySizeInBytes * 8,
            SecretEncoding = SecretEncodings.Spki,
        };
    }

    /// <inheritdoc />
    public SecretKey DeserializeKeyMaterial(
        KeyMetadata metadata,
        string secretEncoding,
        ReadOnlySpan<byte> keyMaterial
    )
    {
        switch (secretEncoding)
        {
            case SecretEncodings.Pkcs8:
            {
                using var key = MLDsa.ImportPkcs8PrivateKey(keyMaterial);
                var publicKey = ExportPublicKey(key);
                return new DefaultAkpSecretKey(
                    DataProtector,
                    metadata,
                    key.Algorithm,
                    Protect(keyMaterial),
                    publicKey,
                    certificateRawData: null
                );
            }

            case SecretEncodings.Spki:
            {
                using var key = MLDsa.ImportSubjectPublicKeyInfo(keyMaterial);
                var publicKey = ExportPublicKey(key);
                return new PublicAkpSecretKey(
                    metadata,
                    key.Algorithm,
                    publicKey,
                    certificateRawData: null
                );
            }

            default:
                throw new InvalidOperationException(
                    $"The '{secretEncoding}' encoding is not supported for an AKP secret."
                );
        }
    }

    /// <inheritdoc />
    public ExportedPublicKey? ExportPublicKey(SecretKey secretKey)
    {
        if (secretKey is not AkpSecretKey akp)
        {
            return null;
        }

        var publicKey = new byte[akp.Algorithm.PublicKeySizeInBytes];
        var writer = new FixedSpanBufferWriter<byte>(publicKey);
        akp.ExportPublicKey(ref writer);

        var jwk = new JsonObject
        {
            [JsonWebKeyParameterNames.KeyType] = JsonWebKeyTypeDescriptors.Akp.Name,
            [JsonWebKeyParameterNames.Algorithm] = akp.AlgorithmName,
            [JsonWebKeyParameterNames.PublicKey] = Base64Url.EncodeToString(publicKey),
        };
        AddIfPresent(jwk, JsonWebKeyParameterNames.KeyId, akp.Metadata.KeyId);
        AddIfPresent(jwk, JsonWebKeyParameterNames.PublicKeyUse, akp.Metadata.Use);

        return new ExportedPublicKey
        {
            Jwk = new JsonWebKey(jwk, JsonWebKeyTypeDescriptors.Akp),
            Pem = akp.ExportSubjectPublicKeyInfoPem(),
        };
    }

    private ReadOnlyMemory<byte> Protect(ReadOnlySpan<byte> material)
    {
        var buffer = BufferFactory.CreateArrayBufferWriter();
        DataProtector.ProtectSpan(material, ref buffer);
        return buffer.WrittenMemory;
    }

    private static byte[] ExportPublicKey(MLDsa key)
    {
        var publicKey = new byte[key.Algorithm.PublicKeySizeInBytes];
        key.ExportMLDsaPublicKey(publicKey);
        return publicKey;
    }

    private static bool TryResolveAlgorithm(
        SecretKeyGenerationParameters parameters,
        [NotNullWhen(true)] out MLDsaAlgorithm? algorithm,
        out string? error
    )
    {
        // The AKP parameter set may be selected by its 'alg' name (carried in the 'Curve' slot) or by key size. A
        // parameter set fixes the size of its keys, so a named parameter set is refused for any other requested size.
        if (parameters.Curve is { } name)
        {
            if (!MlDsaParameterSets.TryGetByName(name, out algorithm))
            {
                error =
                    $"The ML-DSA parameter set '{name}' is not supported; expected 'ML-DSA-44', 'ML-DSA-65', or 'ML-DSA-87'.";
                return false;
            }

            var keySizeBits = algorithm.PublicKeySizeInBytes * 8;
            if (keySizeBits != parameters.KeySizeBits)
            {
                error =
                    $"The ML-DSA key size '{parameters.KeySizeBits}' bits is invalid for the parameter set '{name}', "
                    + $"whose keys are {keySizeBits} bits.";
                algorithm = null;
                return false;
            }

            error = null;
            return true;
        }

        if (MlDsaParameterSets.TryGetByKeySizeBits(parameters.KeySizeBits, out algorithm))
        {
            error = null;
            return true;
        }

        error =
            $"The ML-DSA key size '{parameters.KeySizeBits}' bits does not match a supported parameter set.";
        return false;
    }

    private static byte[] ParseJwkPublicKey(string keyMaterial)
    {
        JsonElement root;
        try
        {
            root = JsonElement.Parse(keyMaterial);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The supplied JWK material is not valid JSON.", exception);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The supplied JWK material is not a JSON object.");
        }

        if (
            !root.TryGetPropertyValue<string>(JsonWebKeyParameterNames.KeyType, out var kty)
            || kty != JsonWebKeyTypeDescriptors.Akp.Name
        )
        {
            throw new ArgumentException("The supplied JWK is not an 'AKP' key.");
        }

        // A JWK carrying any private or secret component is rejected: only public keys may be imported.
        if (new JsonWebKey(root, JsonWebKeyTypeDescriptors.Akp).ContainsPrivateKeyMaterial)
        {
            throw new ArgumentException(
                "Importing private key material is not supported; supply a public JWK."
            );
        }

        if (
            !root.TryGetPropertyValue<string>(JsonWebKeyParameterNames.Algorithm, out var alg)
            || !MlDsaParameterSets.TryGetByName(alg, out var algorithm)
        )
        {
            throw new ArgumentException(
                "The supplied AKP JWK has an unsupported or missing 'alg'."
            );
        }

        if (!root.TryGetPropertyValue<string>(JsonWebKeyParameterNames.PublicKey, out var pub))
        {
            throw new ArgumentException("The supplied AKP JWK is missing the 'pub' member.");
        }

        byte[] publicKey;
        try
        {
            publicKey = Base64Url.DecodeFromChars(pub);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "The AKP JWK 'pub' member is not valid base64url.",
                exception
            );
        }

        if (publicKey.Length != algorithm.PublicKeySizeInBytes)
        {
            throw new ArgumentException(
                $"The AKP JWK 'pub' member has the wrong length for '{algorithm.Name}'."
            );
        }

        using var key = MLDsa.ImportMLDsaPublicKey(algorithm, publicKey);
        return key.ExportSubjectPublicKeyInfo();
    }

    private static byte[] ParsePemPublicKey(string keyMaterial)
    {
        if (!PemEncoding.TryFind(keyMaterial, out var fields))
        {
            throw new ArgumentException("The supplied PEM material is malformed.");
        }

        var label = keyMaterial.AsSpan()[fields.Label];
        if (!label.SequenceEqual("PUBLIC KEY"))
        {
            throw new ArgumentException(
                $"Only 'PUBLIC KEY' PEM material may be imported for AKP keys; got '{label}'."
            );
        }

        try
        {
            using var key = MLDsa.ImportFromPem(keyMaterial);
            return key.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException(
                "The supplied PEM is not a supported AKP public key.",
                exception
            );
        }
    }

    private static void AddIfPresent(JsonObject jwk, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            jwk[name] = value;
        }
    }
}
