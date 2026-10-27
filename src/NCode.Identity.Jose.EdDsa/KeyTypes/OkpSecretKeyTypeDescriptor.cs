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

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Json;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The <see cref="ISecretKeyTypeDescriptor"/> for <c>OKP</c> (Octet Key Pair) Edwards-curve keys (Ed25519 / Ed448).
/// Generates seeds, imports public <c>JWK</c>/<c>PEM</c> material, and deserializes stored <c>PKCS#8</c>/<c>SPKI</c>
/// DER into <see cref="OkpSecretKey"/> instances, data-protecting the private seed in memory.
/// </summary>
/// <param name="dataProtectorFactory">
/// The <see cref="IDataProtectorFactory{T}"/> used to protect and unprotect private seeds at rest in memory.
/// </param>
[PublicAPI]
public sealed class OkpSecretKeyTypeDescriptor(
    IDataProtectorFactory<SecretKey> dataProtectorFactory
) : ISecretKeyTypeDescriptor
{
    private const string OkpJwkKeyType = "OKP";

    private IDataProtector DataProtector { get; } = dataProtectorFactory.CreateDataProtector();

    /// <inheritdoc />
    public string KeyType => OkpSecretKey.TypeName;

    /// <inheritdoc />
    public Type KeyClrType => typeof(OkpSecretKey);

    /// <inheritdoc />
    public string JwkKeyType => OkpJwkKeyType;

    /// <inheritdoc />
    public bool IsAsymmetric => true;

    /// <inheritdoc />
    public bool SupportsPublicKeyExport => true;

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublicKeyImportFormats { get; } =
    [SecretKeyImportFormats.Jwk, SecretKeyImportFormats.Pem];

    /// <inheritdoc />
    public string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        OkpCurves.TryGetByName(parameters.Curve, out _)
            ? null
            : $"The OKP curve '{parameters.Curve}' is not supported; expected '{OkpCurves.Ed25519Name}' or '{OkpCurves.Ed448Name}'.";

    /// <inheritdoc />
    public GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        if (!OkpCurves.TryGetByName(parameters.Curve, out var curve))
        {
            throw new ArgumentException(
                $"The OKP curve '{parameters.Curve}' is not supported.",
                nameof(parameters)
            );
        }

        Span<byte> seed = stackalloc byte[curve.SeedSize];
        RandomNumberGenerator.Fill(seed);
        var pkcs8 = OkpKeyEncoder.BuildPkcs8(curve, seed);
        try
        {
            Span<byte> publicKey = stackalloc byte[curve.PublicKeySize];
            OkpCurves.GeneratePublicKey(curve.Name, seed, publicKey);

            using var subjectPublicKeyInfoBuffer = BufferFactory.CreatePooledBufferWriter(
                isSensitive: false
            );
            IBufferWriter<byte> subjectPublicKeyInfoWriter = subjectPublicKeyInfoBuffer;
            OkpKeyEncoder.WriteSubjectPublicKeyInfo(
                curve,
                publicKey,
                ref subjectPublicKeyInfoWriter
            );
            using var subjectPublicKeyInfo = subjectPublicKeyInfoBuffer.GetSpanLease(
                isSensitive: false
            );

            return new GeneratedSecretKeyMaterial
            {
                ProtectedKeyMaterial = protect(pkcs8),
                KeySizeBits = curve.KeySizeBits,
                SecretEncoding = SecretEncodings.Pkcs8,
                SecretMaterial = null,
                PublicKey = Base64Url.EncodeToString(subjectPublicKeyInfo.Span),
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    /// <inheritdoc />
    public ImportedSecretKeyMaterial ImportPublicKeyMaterial(
        SecretKeyImportParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        var (curve, publicKey) = parameters.Format switch
        {
            SecretKeyImportFormats.Jwk => ParseJwkPublicKey(parameters.KeyMaterial),
            SecretKeyImportFormats.Pem => ParsePemPublicKey(parameters.KeyMaterial),
            _ => throw new ArgumentException(
                $"The import format '{parameters.Format}' is not supported for OKP keys.",
                nameof(parameters)
            ),
        };

        var buffer = BufferFactory.CreateArrayBufferWriter();
        OkpKeyEncoder.WriteSubjectPublicKeyInfo(curve, publicKey, ref buffer);
        return new ImportedSecretKeyMaterial
        {
            ProtectedKeyMaterial = protect(buffer.WrittenSpan),
            KeySizeBits = curve.KeySizeBits,
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
                if (
                    !OkpKeyEncoder.TryParsePkcs8(keyMaterial, out var curve, out var seed)
                    || curve is null
                    || seed is null
                )
                {
                    throw new InvalidOperationException(
                        "The stored OKP PKCS#8 material is malformed."
                    );
                }

                try
                {
                    var publicKey = new byte[curve.PublicKeySize];
                    OkpCurves.GeneratePublicKey(curve.Name, seed, publicKey);
                    return new DefaultOkpSecretKey(
                        DataProtector,
                        metadata,
                        curve,
                        ProtectSeed(seed),
                        publicKey
                    );
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(seed);
                }
            }

            case SecretEncodings.Spki:
            {
                if (
                    !OkpKeyEncoder.TryParseSubjectPublicKeyInfo(
                        keyMaterial,
                        out var curve,
                        out var publicKey
                    )
                    || curve is null
                    || publicKey is null
                )
                {
                    throw new InvalidOperationException(
                        "The stored OKP SPKI material is malformed."
                    );
                }

                return new PublicOkpSecretKey(metadata, curve, publicKey);
            }

            default:
                throw new InvalidOperationException(
                    $"The '{secretEncoding}' encoding is not supported for an OKP secret."
                );
        }
    }

    /// <inheritdoc />
    public ExportedPublicKey? ExportPublicKey(SecretKey secretKey)
    {
        if (secretKey is not OkpSecretKey okp || !OkpCurves.TryGetByName(okp.Curve, out var curve))
        {
            return null;
        }

        Span<byte> publicKey = stackalloc byte[curve.PublicKeySize];
        var writer = new FixedSpanBufferWriter<byte>(publicKey);
        okp.ExportPublicKey(ref writer);

        var jwk = new JsonObject
        {
            ["kty"] = OkpJwkKeyType,
            ["crv"] = okp.Curve,
            ["x"] = Base64Url.EncodeToString(publicKey),
        };
        AddIfPresent(jwk, "kid", okp.Metadata.KeyId);
        AddIfPresent(jwk, "use", okp.Metadata.Use);
        AddIfPresent(jwk, "alg", okp.Metadata.Algorithm);

        return new ExportedPublicKey
        {
            Jwk = new JsonWebKey(jwk),
            Pem = okp.ExportSubjectPublicKeyInfoPem(),
        };
    }

    private ReadOnlyMemory<byte> ProtectSeed(ReadOnlySpan<byte> seed)
    {
        var buffer = BufferFactory.CreateArrayBufferWriter();
        DataProtector.ProtectSpan(seed, ref buffer);
        return buffer.WrittenMemory;
    }

    private static (OkpCurveInfo Curve, byte[] PublicKey) ParseJwkPublicKey(string keyMaterial)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(keyMaterial);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The supplied JWK material is not valid JSON.", exception);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The supplied JWK material is not a JSON object.");
        }

        if (!root.TryGetPropertyValue<string>("kty", out var kty) || kty != OkpJwkKeyType)
        {
            throw new ArgumentException("The supplied JWK is not an 'OKP' key.");
        }

        // A JWK carrying any private or secret component is rejected: only public keys may be imported.
        if (new JsonWebKey(root).ContainsPrivateKeyMaterial)
        {
            throw new ArgumentException(
                "Importing private key material is not supported; supply a public JWK."
            );
        }

        if (
            !root.TryGetPropertyValue<string>("crv", out var crv)
            || !OkpCurves.TryGetByName(crv, out var curve)
        )
        {
            throw new ArgumentException(
                "The supplied OKP JWK has an unsupported or missing 'crv'."
            );
        }

        if (!root.TryGetPropertyValue<string>("x", out var x))
        {
            throw new ArgumentException("The supplied OKP JWK is missing the 'x' member.");
        }

        byte[] publicKey;
        try
        {
            publicKey = Base64Url.DecodeFromChars(x);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "The OKP JWK 'x' member is not valid base64url.",
                exception
            );
        }

        if (publicKey.Length != curve.PublicKeySize)
        {
            throw new ArgumentException(
                $"The OKP JWK 'x' member has the wrong length for curve '{curve.Name}'."
            );
        }

        return (curve, publicKey);
    }

    private static (OkpCurveInfo Curve, byte[] PublicKey) ParsePemPublicKey(string keyMaterial)
    {
        if (!PemEncoding.TryFind(keyMaterial, out var fields))
        {
            throw new ArgumentException("The supplied PEM material is malformed.");
        }

        var label = keyMaterial.AsSpan()[fields.Label];
        if (!label.SequenceEqual("PUBLIC KEY"))
        {
            throw new ArgumentException(
                $"Only 'PUBLIC KEY' PEM material may be imported for OKP keys; got '{label}'."
            );
        }

        var der = new byte[fields.DecodedDataLength];
        if (!Convert.TryFromBase64Chars(keyMaterial.AsSpan()[fields.Base64Data], der, out _))
        {
            throw new ArgumentException("The supplied PEM material is not valid base64.");
        }

        if (
            !OkpKeyEncoder.TryParseSubjectPublicKeyInfo(der, out var curve, out var publicKey)
            || curve is null
            || publicKey is null
        )
        {
            throw new ArgumentException("The supplied PEM is not a supported OKP public key.");
        }

        return (curve, publicKey);
    }

    private static void AddIfPresent(JsonObject jwk, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            jwk[name] = value;
        }
    }
}
