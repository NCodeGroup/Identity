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
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// The built-in <see cref="ISecretKeyTypeDescriptor"/> for <see cref="SecretKeyTypes.Rsa"/> keys. RSA keys are
/// asymmetric and therefore expose exportable and importable public-key material.
/// </summary>
internal sealed class RsaSecretKeyTypeDescriptor(ISecretKeyFactory secretKeyFactory)
    : AsymmetricSecretKeyTypeDescriptor(secretKeyFactory)
{
    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Rsa;

    /// <inheritdoc />
    public override Type KeyClrType => typeof(RsaSecretKey);

    /// <inheritdoc />
    public override JsonWebKeyTypeDescriptor JwkKeyType => JsonWebKeyTypeDescriptors.Rsa;

    /// <inheritdoc />
    public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
        RejectCurveIfPresent(parameters) ?? ValidateKeySizeBits(parameters.KeySizeBits);

    private static string? ValidateKeySizeBits(int keySizeBits) =>
        keySizeBits < 2048 || keySizeBits > 16384 || keySizeBits % 8 != 0
            ? $"The RSA key size '{keySizeBits}' bits is invalid; expected a multiple of 8 "
                + "between 2048 and 16384 bits."
            : null;

    /// <inheritdoc />
    public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
        SecretKeyGenerationParameters parameters,
        SecretKeyMaterialProtector protect
    )
    {
        using var rsa = RSA.Create(parameters.KeySizeBits);
        return ExportProtectedPkcs8(rsa, protect);
    }

    /// <inheritdoc />
    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ParseJwkPublicKey(
        JsonElement root
    )
    {
        var parameters = new RSAParameters
        {
            Modulus = GetRequiredBase64UrlMember(root, "n"),
            Exponent = GetRequiredBase64UrlMember(root, "e"),
        };

        try
        {
            using var rsa = RSA.Create(parameters);
            return (rsa.ExportSubjectPublicKeyInfo(), rsa.KeySize);
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("The supplied RSA JWK is malformed.", exception);
        }
    }

    /// <inheritdoc />
    protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ImportPublicKeyPem(string pem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return (rsa.ExportSubjectPublicKeyInfo(), rsa.KeySize);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "The supplied public key PEM is not a valid RSA key.",
                exception
            );
        }
    }

    /// <inheritdoc />
    protected override bool TryGetCertificateKeySizeBits(
        X509Certificate2 certificate,
        out int keySizeBits
    )
    {
        using var rsa = certificate.GetRSAPublicKey();
        keySizeBits = rsa?.KeySize ?? 0;
        return rsa is not null;
    }

    /// <inheritdoc />
    protected override SecretKey CreatePrivateKeyFromPkcs8(
        KeyMetadata metadata,
        ReadOnlySpan<byte> pkcs8
    ) => SecretKeyFactory.CreateRsaPkcs8(metadata, pkcs8);

    /// <inheritdoc />
    protected override SecretKey CreatePublicKeyFromSpki(
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    ) => SecretKeyFactory.CreateRsaSpki(metadata, subjectPublicKeyInfo);

    /// <inheritdoc />
    protected override JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey)
    {
        using var rsa = ((RsaSecretKey)secretKey).ExportRSA();
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var modulus =
            parameters.Modulus
            ?? throw new InvalidOperationException("The RSA key is missing its modulus.");
        var exponent =
            parameters.Exponent
            ?? throw new InvalidOperationException("The RSA key is missing its exponent.");
        return new JsonObject
        {
            ["kty"] = JwkKeyType.Name,
            ["n"] = Base64Url.EncodeToString(modulus),
            ["e"] = Base64Url.EncodeToString(exponent),
        };
    }
}
