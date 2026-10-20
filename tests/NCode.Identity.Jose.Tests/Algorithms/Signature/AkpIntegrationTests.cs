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

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.Algorithms.Signature;

/// <summary>
/// End-to-end tests over the AKP secret-key descriptor and the <c>ML-DSA</c> (FIPS 204 / RFC 9964) signature
/// algorithm: generate → sign → verify, and public-key export/re-import round-trips through JWK and PEM. The tests
/// no-op where the running OS does not provide ML-DSA (<see cref="MLDsa.IsSupported"/>).
/// </summary>
public class AkpIntegrationTests
{
    #region Helpers

    private static readonly SecretKeyMaterialProtector Protect = bytes =>
        Convert.ToBase64String(bytes);

    private static AkpSecretKeyTypeDescriptor CreateDescriptor() =>
        new(new IdentityDataProtectorFactory());

    private static MLDsaAlgorithm Resolve(string name) =>
        name switch
        {
            "ML-DSA-44" => MLDsaAlgorithm.MLDsa44,
            "ML-DSA-65" => MLDsaAlgorithm.MLDsa65,
            "ML-DSA-87" => MLDsaAlgorithm.MLDsa87,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static MlDsaSignatureAlgorithm CreateAlgorithm(string name) =>
        new(name, Resolve(name), HashAlgorithmName.SHA256);

    private static int KeySizeBitsOf(string name) => Resolve(name).PublicKeySizeInBytes * 8;

    private static AkpSecretKey GeneratePrivateKey(
        AkpSecretKeyTypeDescriptor descriptor,
        string name
    )
    {
        var parameters = new SecretKeyGenerationParameters
        {
            KeySizeBits = KeySizeBitsOf(name),
            Curve = name,
        };
        var generated = descriptor.GenerateKeyMaterial(parameters, Protect);

        Assert.Equal(SecretEncodings.Pkcs8, generated.SecretEncoding);

        var pkcs8 = Convert.FromBase64String(generated.ProtectedKeyMaterial);
        var metadata = new KeyMetadata { KeyId = "kid-test", Use = "sig" };
        var secretKey = descriptor.DeserializeKeyMaterial(metadata, SecretEncodings.Pkcs8, pkcs8);

        var akpSecretKey = Assert.IsType<AkpSecretKey>(secretKey, exactMatch: false);
        Assert.True(akpSecretKey.HasPrivateKey);
        Assert.Equal(name, akpSecretKey.AlgorithmName);
        return akpSecretKey;
    }

    private static AkpSecretKey ImportPublicKey(
        AkpSecretKeyTypeDescriptor descriptor,
        SecretKeyImportFormat format,
        string keyMaterial
    )
    {
        var parameters = new SecretKeyImportParameters
        {
            Format = format,
            KeyMaterial = keyMaterial,
        };
        var imported = descriptor.ImportPublicKeyMaterial(parameters, Protect);

        Assert.Equal(SecretEncodings.Spki, imported.SecretEncoding);

        var spki = Convert.FromBase64String(imported.ProtectedKeyMaterial);
        var metadata = new KeyMetadata { KeyId = "kid-test", Use = "sig" };
        var secretKey = descriptor.DeserializeKeyMaterial(metadata, SecretEncodings.Spki, spki);
        return Assert.IsType<AkpSecretKey>(secretKey, exactMatch: false);
    }

    #endregion

    #region GenerateSignVerify

    [Theory]
    [InlineData("ML-DSA-44")]
    [InlineData("ML-DSA-65")]
    [InlineData("ML-DSA-87")]
    public void GenerateSignVerify_RoundTrips(string name)
    {
        if (!MLDsa.IsSupported)
            return;

        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm(name);
        var secretKey = GeneratePrivateKey(descriptor, name);

        var message = "the quick brown fox"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(KeySizeBitsOf(name))];

        var signed = algorithm.TrySign(secretKey, message, signature, out var bytesWritten);

        Assert.True(signed);
        Assert.Equal(signature.Length, bytesWritten);
        Assert.True(algorithm.Verify(secretKey, message, signature));
    }

    [Theory]
    [InlineData("ML-DSA-44")]
    [InlineData("ML-DSA-65")]
    [InlineData("ML-DSA-87")]
    public void Verify_WhenMessageTampered_ReturnsFalse(string name)
    {
        if (!MLDsa.IsSupported)
            return;

        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm(name);
        var secretKey = GeneratePrivateKey(descriptor, name);

        var message = "authentic payload"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(KeySizeBitsOf(name))];
        Assert.True(algorithm.TrySign(secretKey, message, signature, out _));

        var tampered = (byte[])message.Clone();
        tampered[0] ^= 0xFF;

        Assert.False(algorithm.Verify(secretKey, tampered, signature));
    }

    #endregion

    #region PublicKeyExportImport

    [Theory]
    [InlineData("ML-DSA-44")]
    [InlineData("ML-DSA-65")]
    [InlineData("ML-DSA-87")]
    public void ExportImportJwk_VerifiesSignatureWithPublicKey(string name)
    {
        if (!MLDsa.IsSupported)
            return;

        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm(name);
        var privateKey = GeneratePrivateKey(descriptor, name);

        var message = "jwk round-trip"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(KeySizeBitsOf(name))];
        Assert.True(algorithm.TrySign(privateKey, message, signature, out _));

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);
        Assert.Equal("AKP", exported.Jwk.KeyType);
        Assert.Equal(name, exported.Jwk.Algorithm);

        var publicKey = ImportPublicKey(
            descriptor,
            SecretKeyImportFormat.Jwk,
            System.Text.Json.JsonSerializer.Serialize(exported.Jwk)
        );

        Assert.False(publicKey.HasPrivateKey);
        Assert.True(algorithm.Verify(publicKey, message, signature));
    }

    [Theory]
    [InlineData("ML-DSA-44")]
    [InlineData("ML-DSA-65")]
    [InlineData("ML-DSA-87")]
    public void ExportImportPem_VerifiesSignatureWithPublicKey(string name)
    {
        if (!MLDsa.IsSupported)
            return;

        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm(name);
        var privateKey = GeneratePrivateKey(descriptor, name);

        var message = "pem round-trip"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(KeySizeBitsOf(name))];
        Assert.True(algorithm.TrySign(privateKey, message, signature, out _));

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);

        var publicKey = ImportPublicKey(descriptor, SecretKeyImportFormat.Pem, exported.Pem);

        Assert.False(publicKey.HasPrivateKey);
        Assert.True(algorithm.Verify(publicKey, message, signature));
    }

    [Theory]
    [InlineData("ML-DSA-65")]
    public void TrySign_WhenPublicOnlyKey_Throws(string name)
    {
        if (!MLDsa.IsSupported)
            return;

        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm(name);
        var privateKey = GeneratePrivateKey(descriptor, name);

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);
        var publicKey = ImportPublicKey(
            descriptor,
            SecretKeyImportFormat.Jwk,
            System.Text.Json.JsonSerializer.Serialize(exported.Jwk)
        );

        var signature = new byte[algorithm.GetSignatureSizeBytes(KeySizeBitsOf(name))];

        Assert.Throws<InvalidOperationException>(() =>
            algorithm.TrySign(publicKey, "x"u8, signature, out _)
        );
    }

    [Fact]
    public void JwkKeyType_IsAkp()
    {
        Assert.Equal("AKP", CreateDescriptor().JwkKeyType);
    }

    [Theory]
    [InlineData("priv")]
    [InlineData("d")]
    public void ImportJwk_WhenPrivateMemberPresent_Throws(string privateMember)
    {
        var descriptor = CreateDescriptor();
        var jwk = $$"""{"kty":"AKP","alg":"ML-DSA-44","pub":"AAAA","{{privateMember}}":"AAAA"}""";
        var parameters = new SecretKeyImportParameters
        {
            Format = SecretKeyImportFormat.Jwk,
            KeyMaterial = jwk,
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            descriptor.ImportPublicKeyMaterial(parameters, Protect)
        );

        Assert.Contains("private key material", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region IdentityDataProtector

    private sealed class IdentityDataProtectorFactory : IDataProtectorFactory<SecretKey>
    {
        public IDataProtector CreateDataProtector() => new IdentityDataProtector();
    }

    private sealed class IdentityDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => (byte[])plaintext.Clone();

        public byte[] Unprotect(byte[] protectedData) => (byte[])protectedData.Clone();
    }

    #endregion
}
