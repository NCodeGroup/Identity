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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// Provides the common import and deserialization logic shared by the asymmetric (RSA / ECC) secret-key-type
/// descriptors: the <c>JWK</c> / <c>PEM</c> / certificate import dispatch (public-only), and the
/// <c>PKCS#8</c> / <c>SPKI</c> / <c>X.509</c> deserialization dispatch. Family-specific parsing is delegated to the
/// abstract hooks a concrete RSA or ECC descriptor implements. Applications can derive from this base to add support
/// for custom asymmetric key types.
/// </summary>
// See ADR-0104.
[PublicAPI]
public abstract class AsymmetricSecretKeyTypeDescriptor(ISecretKeyFactory secretKeyFactory)
    : SecretKeyTypeDescriptor
{
    /// <summary>
    /// Gets the factory used to build runtime <see cref="SecretKey"/> instances during deserialization.
    /// </summary>
    protected ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    /// <summary>
    /// Gets the <c>JWK</c> <c>kty</c> value this key type accepts (for example <c>RSA</c> or <c>EC</c>).
    /// </summary>
    protected abstract string ExpectedJwkKeyType { get; }

    /// <inheritdoc />
    public override ImportedSecretKeyMaterial ImportPublicKeyMaterial(
        SecretKeyImportParameters parameters,
        SecretKeyMaterialProtector protect
    ) =>
        parameters.Format switch
        {
            SecretKeyImportFormat.Jwk => ImportJwk(parameters.KeyMaterial, protect),
            SecretKeyImportFormat.Pem => ImportPem(parameters.KeyMaterial, protect),
            SecretKeyImportFormat.Certificate => ImportCertificate(parameters.KeyMaterial, protect),
            _ => throw new ArgumentException(
                $"The import format '{parameters.Format}' is not supported.",
                nameof(parameters)
            ),
        };

    /// <inheritdoc />
    public override SecretKey DeserializeKeyMaterial(
        KeyMetadata metadata,
        string secretEncoding,
        ReadOnlySpan<byte> keyMaterial
    ) =>
        secretEncoding switch
        {
            SecretEncodings.Pkcs8 => CreatePrivateKeyFromPkcs8(metadata, keyMaterial),
            SecretEncodings.Spki => CreatePublicKeyFromSpki(metadata, keyMaterial),
            SecretEncodings.X509 => DeserializeCertificate(metadata, keyMaterial),
            _ => throw new InvalidOperationException(
                $"The '{secretEncoding}' encoding is not supported for a '{KeyType}' secret."
            ),
        };

    /// <inheritdoc />
    public override ExportedPublicKey? ExportPublicKey(SecretKey secretKey)
    {
        if (secretKey is not AsymmetricSecretKey asymmetricSecretKey)
            return null;

        var jwk = WritePublicKeyJwk(asymmetricSecretKey);
        if (jwk is null)
            return null;

        var metadata = asymmetricSecretKey.Metadata;
        AddIfPresent(jwk, "kid", metadata.KeyId);
        AddIfPresent(jwk, "use", metadata.Use);
        AddIfPresent(jwk, "alg", metadata.Algorithm);

        return new ExportedPublicKey
        {
            Jwk = jwk,
            Pem = asymmetricSecretKey.ExportSubjectPublicKeyInfoPem(),
        };
    }

    private static void AddIfPresent(JsonObject jwk, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            jwk[name] = value;
    }

    private ImportedSecretKeyMaterial ImportJwk(
        string keyMaterial,
        SecretKeyMaterialProtector protect
    )
    {
        JsonElement root;
        try
        {
            root = JsonElement.Parse(keyMaterial);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "The supplied JWK material is not valid JSON.",
                nameof(keyMaterial),
                exception
            );
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                "The supplied JWK material is not a JSON object.",
                nameof(keyMaterial)
            );

        // A JWK carrying a private component is rejected: only public keys may be imported.
        if (root.TryGetProperty("d", out _))
            throw new ArgumentException(
                "Importing private key material is not supported; supply a public JWK.",
                nameof(keyMaterial)
            );

        if (
            !root.TryGetProperty("kty", out var ktyElement)
            || ktyElement.ValueKind != JsonValueKind.String
        )
            throw new ArgumentException(
                "The supplied JWK is missing the 'kty' member.",
                nameof(keyMaterial)
            );

        var kty = ktyElement.GetString();
        if (!string.Equals(kty, ExpectedJwkKeyType, StringComparison.Ordinal))
            throw new ArgumentException(
                $"The JWK key type '{kty}' does not match the expected '{ExpectedJwkKeyType}' for a "
                    + $"'{KeyType}' secret.",
                nameof(keyMaterial)
            );

        var (subjectPublicKeyInfo, keySizeBits) = ParseJwkPublicKey(root);
        return Spki(subjectPublicKeyInfo, keySizeBits, protect);
    }

    private ImportedSecretKeyMaterial ImportPem(
        string keyMaterial,
        SecretKeyMaterialProtector protect
    )
    {
        if (!PemEncoding.TryFind(keyMaterial, out var fields))
            throw new ArgumentException(
                "The supplied PEM material is malformed.",
                nameof(keyMaterial)
            );

        var label = keyMaterial.AsSpan()[fields.Label];
        if (label.SequenceEqual("CERTIFICATE"))
            return ImportCertificate(keyMaterial, protect);

        if (!label.SequenceEqual("PUBLIC KEY"))
            throw new ArgumentException(
                $"Only 'PUBLIC KEY' or 'CERTIFICATE' PEM material may be imported; got '{label}'.",
                nameof(keyMaterial)
            );

        var (subjectPublicKeyInfo, keySizeBits) = ImportPublicKeyPem(keyMaterial);
        return Spki(subjectPublicKeyInfo, keySizeBits, protect);
    }

    private ImportedSecretKeyMaterial ImportCertificate(
        string keyMaterial,
        SecretKeyMaterialProtector protect
    )
    {
        using var certificate = LoadCertificate(keyMaterial);
        if (!TryGetCertificateKeySizeBits(certificate, out var keySizeBits))
            throw new ArgumentException(
                $"The certificate's public key is not a '{KeyType}' key.",
                nameof(keyMaterial)
            );

        var certificateDer = certificate.Export(X509ContentType.Cert);
        return new ImportedSecretKeyMaterial
        {
            ProtectedKeyMaterial = protect(certificateDer),
            KeySizeBits = keySizeBits,
            SecretEncoding = SecretEncodings.X509,
        };
    }

    private static ImportedSecretKeyMaterial Spki(
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        int keySizeBits,
        SecretKeyMaterialProtector protect
    ) =>
        new()
        {
            ProtectedKeyMaterial = protect(subjectPublicKeyInfo),
            KeySizeBits = keySizeBits,
            SecretEncoding = SecretEncodings.Spki,
        };

    private AsymmetricSecretKey DeserializeCertificate(
        KeyMetadata metadata,
        ReadOnlySpan<byte> keyMaterial
    )
    {
        using var certificate = X509CertificateLoader.LoadCertificate(keyMaterial);
        return SecretKeyFactory.Create(metadata, certificate);
    }

    private static X509Certificate2 LoadCertificate(string keyMaterial)
    {
        try
        {
            if (keyMaterial.Contains("-----BEGIN", StringComparison.Ordinal))
                return X509Certificate2.CreateFromPem(keyMaterial);

            return X509CertificateLoader.LoadCertificate(
                Convert.FromBase64String(keyMaterial.Trim())
            );
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new ArgumentException(
                "The supplied certificate material is malformed.",
                nameof(keyMaterial),
                exception
            );
        }
    }

    /// <summary>
    /// Decodes a required base64url <c>JWK</c> member, throwing <see cref="ArgumentException"/> when it is absent or
    /// malformed.
    /// </summary>
    protected static byte[] GetRequiredBase64UrlMember(JsonElement root, string memberName)
    {
        if (
            !root.TryGetProperty(memberName, out var element)
            || element.ValueKind != JsonValueKind.String
        )
            throw new ArgumentException($"The supplied JWK is missing the '{memberName}' member.");

        try
        {
            return Base64Url.DecodeFromChars(element.GetString());
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"The JWK '{memberName}' member is not valid base64url.",
                exception
            );
        }
    }

    /// <summary>
    /// Parses the family-specific public-key members of a validated <c>JWK</c> into <c>SubjectPublicKeyInfo</c>
    /// (SPKI) DER and the key size in bits.
    /// </summary>
    protected abstract (byte[] SubjectPublicKeyInfo, int KeySizeBits) ParseJwkPublicKey(
        JsonElement root
    );

    /// <summary>
    /// Imports a family-specific <c>PUBLIC KEY</c> PEM into <c>SubjectPublicKeyInfo</c> (SPKI) DER and the key size
    /// in bits.
    /// </summary>
    protected abstract (byte[] SubjectPublicKeyInfo, int KeySizeBits) ImportPublicKeyPem(
        string pem
    );

    /// <summary>
    /// Returns the key size in bits when the certificate's public key matches this descriptor's family; otherwise
    /// returns <c>false</c>.
    /// </summary>
    protected abstract bool TryGetCertificateKeySizeBits(
        X509Certificate2 certificate,
        out int keySizeBits
    );

    /// <summary>
    /// Builds the runtime <see cref="SecretKey"/> from a private <c>PKCS#8</c> key.
    /// </summary>
    protected abstract SecretKey CreatePrivateKeyFromPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> pkcs8
    );

    /// <summary>
    /// Builds the runtime verify-only <see cref="SecretKey"/> from a public <c>SubjectPublicKeyInfo</c> (SPKI) key.
    /// </summary>
    protected abstract SecretKey CreatePublicKeyFromSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    );

    /// <summary>
    /// Writes the family-specific public <c>JSON Web Key</c> members (<c>kty</c> plus the public key material) for
    /// the specified key, or returns <c>null</c> when the key cannot be represented (for example an unsupported
    /// elliptic curve). The common <c>kid</c>/<c>use</c>/<c>alg</c> members are added by the base.
    /// </summary>
    protected abstract JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey);
}
