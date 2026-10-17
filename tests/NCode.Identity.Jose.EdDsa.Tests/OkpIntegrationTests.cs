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

using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jose.EdDsa.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.Logic;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using Xunit;

namespace NCode.Identity.Jose.EdDsa.Tests;

/// <summary>
/// End-to-end tests over the OKP secret-key descriptor and the <c>EdDSA</c> signature algorithm: generate → sign →
/// verify, and public-key export/re-import round-trips through JWK and PEM.
/// </summary>
public class OkpIntegrationTests
{
    #region Helpers

    private static readonly SecretKeyMaterialProtector Protect = bytes =>
        Convert.ToBase64String(bytes);

    private static OkpSecretKeyTypeDescriptor CreateDescriptor() =>
        new(new IdentityDataProtectorFactory());

    private static EdDsaSignatureAlgorithm CreateAlgorithm() => new();

    private static OkpSecretKey GeneratePrivateKey(
        OkpSecretKeyTypeDescriptor descriptor,
        string curve,
        int keySizeBits
    )
    {
        var parameters = new SecretKeyGenerationParameters
        {
            KeySizeBits = keySizeBits,
            Curve = curve,
        };
        var generated = descriptor.GenerateKeyMaterial(parameters, Protect);

        Assert.Equal(SecretEncodings.Pkcs8, generated.SecretEncoding);

        var pkcs8 = Convert.FromBase64String(generated.ProtectedKeyMaterial);
        var metadata = new KeyMetadata { KeyId = "kid-test", Use = "sig" };
        var secretKey = descriptor.DeserializeKeyMaterial(metadata, SecretEncodings.Pkcs8, pkcs8);

        var okpSecretKey = Assert.IsType<OkpSecretKey>(secretKey, exactMatch: false);
        Assert.True(okpSecretKey.HasPrivateKey);
        return okpSecretKey;
    }

    #endregion

    #region GenerateSignVerify

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void GenerateSignVerify_RoundTrips(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var secretKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var message = "the quick brown fox"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];

        var signed = algorithm.TrySign(secretKey, message, signature, out var bytesWritten);

        Assert.True(signed);
        Assert.Equal(signature.Length, bytesWritten);
        Assert.True(algorithm.Verify(secretKey, message, signature));
    }

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void Verify_WhenMessageTampered_ReturnsFalse(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var secretKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var message = "authentic payload"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];
        Assert.True(algorithm.TrySign(secretKey, message, signature, out _));

        var tampered = (byte[])message.Clone();
        tampered[0] ^= 0xFF;

        Assert.False(algorithm.Verify(secretKey, tampered, signature));
    }

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void Verify_WhenSignatureTampered_ReturnsFalse(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var secretKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var message = "authentic payload"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];
        Assert.True(algorithm.TrySign(secretKey, message, signature, out _));

        signature[^1] ^= 0xFF;

        Assert.False(algorithm.Verify(secretKey, message, signature));
    }

    #endregion

    #region PublicKeyExportImport

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void ExportImportJwk_VerifiesSignatureWithPublicKey(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var privateKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var message = "jwk round-trip"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];
        Assert.True(algorithm.TrySign(privateKey, message, signature, out _));

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);

        var publicKey = ImportPublicKey(
            descriptor,
            SecretKeyImportFormat.Jwk,
            exported.Jwk.ToJsonString()
        );

        Assert.False(publicKey.HasPrivateKey);
        Assert.True(algorithm.Verify(publicKey, message, signature));
    }

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void ExportImportPem_VerifiesSignatureWithPublicKey(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var privateKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var message = "pem round-trip"u8.ToArray();
        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];
        Assert.True(algorithm.TrySign(privateKey, message, signature, out _));

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);

        var publicKey = ImportPublicKey(descriptor, SecretKeyImportFormat.Pem, exported.Pem);

        Assert.False(publicKey.HasPrivateKey);
        Assert.True(algorithm.Verify(publicKey, message, signature));
    }

    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.Ed448Name, 456)]
    public void TrySign_WhenPublicOnlyKey_Throws(string curve, int keySizeBits)
    {
        var descriptor = CreateDescriptor();
        var algorithm = CreateAlgorithm();
        var privateKey = GeneratePrivateKey(descriptor, curve, keySizeBits);

        var exported = descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);
        var publicKey = ImportPublicKey(
            descriptor,
            SecretKeyImportFormat.Jwk,
            exported.Jwk.ToJsonString()
        );

        var signature = new byte[algorithm.GetSignatureSizeBytes(keySizeBits)];

        Assert.Throws<InvalidOperationException>(() =>
            algorithm.TrySign(publicKey, "x"u8, signature, out _)
        );
    }

    private static OkpSecretKey ImportPublicKey(
        OkpSecretKeyTypeDescriptor descriptor,
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
        return Assert.IsType<OkpSecretKey>(secretKey, exactMatch: false);
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
