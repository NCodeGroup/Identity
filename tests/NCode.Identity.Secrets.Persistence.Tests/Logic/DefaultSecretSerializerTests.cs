#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

public class DefaultSecretSerializerTests
{
    private readonly IDataProtector _serializerProtector;
    private readonly DefaultSecretSerializer _serializer;

    public DefaultSecretSerializerTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        _serializerProtector = provider.CreateProtector("serializer");
        var factoryProtector = provider.CreateProtector("factory");

        var secretKeyFactory = new DefaultSecretKeyFactory(
            new TestDataProtectorFactory<SecretKey>(factoryProtector)
        );

        _serializer = new DefaultSecretSerializer(
            secretKeyFactory,
            new TestDataProtectorFactory<PersistedSecret>(_serializerProtector)
        );
    }

    private string EncodeProtected(ReadOnlySpan<byte> rawBytes)
    {
        var writer = new ArrayBufferWriter<byte>();
        _serializerProtector.ProtectSpan(rawBytes, ref writer);
        return Base64Url.EncodeToString(writer.WrittenSpan);
    }

    private PersistedSecret CreatePersistedSecret(
        string secretType,
        int keySizeBits,
        ReadOnlySpan<byte> rawBytes,
        string secretId = "secret-1",
        DateTimeOffset? expiresWhen = null
    ) =>
        new()
        {
            SecretId = secretId,
            Use = "sig",
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = expiresWhen ?? DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            EncodedValue = EncodeProtected(rawBytes),
        };

    #region DeserializeSecret Symmetric Tests

    [Fact]
    public void DeserializeSecret_WhenSymmetric_ReturnsSymmetricKey()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var persisted = CreatePersistedSecret(
            SecretTypes.Symmetric,
            keyMaterial.Length * 8,
            keyMaterial
        );

        var secretKey = _serializer.DeserializeSecret(persisted);

        var symmetric = Assert.IsAssignableFrom<SymmetricSecretKey>(secretKey);
        Assert.Equal("secret-1", symmetric.KeyId);
        Assert.Equal(keyMaterial.Length * 8, symmetric.KeySizeBits);

        var writer = new ArrayBufferWriter<byte>();
        symmetric.ExportPrivateKey(ref writer);
        Assert.Equal(keyMaterial, writer.WrittenSpan.ToArray());
    }

    #endregion

    #region DeserializeSecret RSA Tests

    [Fact]
    public void DeserializeSecret_WhenRsa_ReturnsRsaKey()
    {
        using var rsa = RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var expectedModulus = rsa.ExportParameters(false).Modulus;

        var persisted = CreatePersistedSecret(SecretTypes.Rsa, rsa.KeySize, pkcs8);

        var secretKey = _serializer.DeserializeSecret(persisted);

        var rsaKey = Assert.IsAssignableFrom<RsaSecretKey>(secretKey);
        using var exported = rsaKey.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    #endregion

    #region DeserializeSecret ECC Tests

    [Fact]
    public void DeserializeSecret_WhenEcc_ReturnsEccKey()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        var pkcs8 = ecdh.ExportPkcs8PrivateKey();

        var persisted = CreatePersistedSecret(SecretTypes.Ecc, ecdh.KeySize, pkcs8);

        var secretKey = _serializer.DeserializeSecret(persisted);

        var eccKey = Assert.IsAssignableFrom<EccSecretKey>(secretKey);
        Assert.Equal(384, eccKey.KeySizeBits);
        using var exported = eccKey.ExportECDiffieHellman();
        Assert.Equal(384, exported.KeySize);
    }

    #endregion

    #region DeserializeSecret Error Tests

    [Fact]
    public void DeserializeSecret_WhenUnsupportedType_ThrowsInvalidOperation()
    {
        var persisted = CreatePersistedSecret(
            "unsupported",
            128,
            [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _serializer.DeserializeSecret(persisted)
        );

        Assert.Contains("unsupported", exception.Message);
    }

    #endregion

    #region DeserializeSecrets Tests

    [Fact]
    public void DeserializeSecrets_WhenMultiple_ReturnsAllSortedByExpiresDescending()
    {
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        var earlier = CreatePersistedSecret(
            SecretTypes.Symmetric,
            keyMaterial.Length * 8,
            keyMaterial,
            secretId: "earlier",
            expiresWhen: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var later = CreatePersistedSecret(
            SecretTypes.Symmetric,
            keyMaterial.Length * 8,
            keyMaterial,
            secretId: "later",
            expiresWhen: new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );

        var secretKeys = _serializer.DeserializeSecrets([earlier, later]);

        Assert.Equal(2, secretKeys.Count);
        Assert.Equal(["later", "earlier"], secretKeys.Select(x => x.KeyId));
    }

    [Fact]
    public void DeserializeSecrets_WhenEmpty_ReturnsEmpty()
    {
        var secretKeys = _serializer.DeserializeSecrets([]);

        Assert.Empty(secretKeys);
    }

    #endregion
}
