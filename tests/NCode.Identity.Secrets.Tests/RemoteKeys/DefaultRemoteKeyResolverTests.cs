// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1476.
// See ADR-1479.
public class DefaultRemoteKeyResolverTests : BaseTests
{
    private const string KeyId = "key-1";

    private static RemoteSigningAlgorithm RsaRow { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
        };

    // Asks for a larger key than the 2048-bit key the tests make the provider return.
    private static RemoteSigningAlgorithm LargeRsaRow { get; } =
        RsaRow with
        {
            KeySizeBits = 4096,
            KeySpec = "RSA_4096",
        };

    private static RemoteSigningAlgorithm EccRow { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            IsDerEncodedSignature = true,
        };

    // Asks for a curve other than the P-256 curve of the key the tests make the provider return.
    private static RemoteSigningAlgorithm OtherCurveEccRow { get; } =
        EccRow with
        {
            Curve = "secp256k1",
            KeySpec = "ECC_SECG_P256K1",
        };

    private static RemoteDecryptionAlgorithm OaepRow { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private Mock<IRemoteKeyProvider> Provider { get; }

    private DefaultRemoteKeyResolver Resolver { get; }

    public DefaultRemoteKeyResolverTests()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var dataProtectorFactory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        dataProtectorFactory
            .Setup(x => x.CreateDataProtector())
            .Returns(dataProtectionProvider.CreateProtector("test"))
            .Verifiable();
        var eccCurveCatalog = new DefaultEccCurveCatalog([]);
        var secretKeyFactory = new DefaultSecretKeyFactory(
            dataProtectorFactory.Object,
            eccCurveCatalog
        );
        var secretKeyTypeCatalog = new DefaultSecretKeyTypeCatalog([
            new SymmetricSecretKeyTypeDescriptor(secretKeyFactory),
            new RsaSecretKeyTypeDescriptor(secretKeyFactory),
            new EccSecretKeyTypeDescriptor(secretKeyFactory, eccCurveCatalog),
        ]);

        Provider = CreateStrictMock<IRemoteKeyProvider>();
        Provider.Setup(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();

        var source = CreateStrictMock<IRemoteKeyDescriptorSource>();
        source
            .Setup(x => x.Providers)
            .Returns([new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }])
            .Verifiable();
        source
            .Setup(x => x.Algorithms)
            .Returns([RsaRow, LargeRsaRow, EccRow, OtherCurveEccRow, OaepRow])
            .Verifiable();
        var descriptorCatalog = new DefaultRemoteKeyDescriptorCatalog([source.Object]);
        var providerCatalog = new DefaultRemoteKeyProviderCatalog(
            [Provider.Object],
            descriptorCatalog
        );

        Resolver = new DefaultRemoteKeyResolver(
            secretKeyTypeCatalog,
            providerCatalog,
            descriptorCatalog
        );
    }

    private static RemoteKeyAlgorithmSelector CreateSelector(
        string secretType = SecretKeyTypes.Rsa,
        int keySizeBits = 2048,
        string? curve = null,
        string algorithm = "RS256",
        string provider = RemoteKeyProviderNames.AwsKms
    ) =>
        new()
        {
            Provider = provider,
            Algorithm = algorithm,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
        };

    private static byte[] CreateRsaSubjectPublicKeyInfo()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfo();
    }

    private static byte[] CreateEccSubjectPublicKeyInfo()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportSubjectPublicKeyInfo();
    }

    private RemoteKeyBinding CreateBinding(RemoteKeyAlgorithm row) =>
        new()
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, KeyId),
            Algorithm = row,
        };

    private Moq.Language.Flow.ISetup<IRemoteKeyProvider, ValueTask> SetupGetPublicKeyCall(
        RemoteKeyAlgorithm row
    ) =>
        Provider.Setup(x =>
            x.GetPublicKeyAsync(
                It.Is<RemotePublicKeyRequest>(request =>
                    request.KeyId == KeyId && ReferenceEquals(request.Algorithm, row)
                ),
                It.IsAny<IBufferWriter<byte>>(),
                CancellationToken.None
            )
        );

    private void SetupGetPublicKey(RemoteKeyAlgorithm row, byte[] subjectPublicKeyInfo) =>
        SetupGetPublicKeyCall(row)
            .Callback(
                (RemotePublicKeyRequest _, IBufferWriter<byte> writer, CancellationToken _) =>
                    writer.Write(subjectPublicKeyInfo)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region Resolve Tests

    [Theory]
    [InlineData("unknown")]
    [InlineData(SecretKeyTypes.Symmetric)]
    public void Resolve_WhenTheSecretTypeCannotBeHeldRemotely_Throws(string secretType)
    {
        var selector = CreateSelector(secretType);

        var exception = Assert.Throws<RemoteKeyRefusedException>(() => Resolver.Resolve(selector));

        Assert.Equal(
            $"The '{secretType}' secret type cannot be held in a key management service.",
            exception.Message
        );
    }

    [Fact]
    public void Resolve_WhenTheProviderIsNotRegistered_Throws()
    {
        var selector = CreateSelector(provider: "unregistered-kms");

        var exception = Assert.Throws<RemoteKeyRefusedException>(() => Resolver.Resolve(selector));

        Assert.Equal(
            "The remote key provider 'unregistered-kms' is not registered.",
            exception.Message
        );
    }

    [Fact]
    public void Resolve_WhenNoRowMatches_Throws()
    {
        var selector = CreateSelector(algorithm: "RS384");

        var exception = Assert.Throws<RemoteKeyRefusedException>(() => Resolver.Resolve(selector));

        Assert.Equal(
            "The remote key provider 'aws-kms' has no algorithm 'RS384' for the 'rsa' secret type, key size 2048 and curve ''.",
            exception.Message
        );
    }

    [Fact]
    public void Resolve_WhenARowMatches_ReturnsTheProviderAndRow()
    {
        var selector = CreateSelector(SecretKeyTypes.Ecc, 256, "P-256", "ES256");

        var resolution = Resolver.Resolve(selector);

        Assert.Same(Provider.Object, resolution.Provider);
        Assert.Same(EccRow, resolution.Algorithm);
    }

    #endregion

    #region ReadKeyAsync Tests

    [Fact]
    public async Task ReadKeyAsync_WhenRsa_ReturnsARemoteKeyWithTheRowsUseAndAlgorithm()
    {
        var spki = CreateRsaSubjectPublicKeyInfo();
        SetupGetPublicKey(RsaRow, spki);
        var binding = CreateBinding(RsaRow);

        var key = await Resolver.ReadKeyAsync(
            binding,
            new KeyMetadata { KeyId = "secret-1" },
            CancellationToken.None
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
        Assert.Equal(SecretKeyTypes.Rsa, key.KeyType);
        Assert.Equal(2048, key.KeySizeBits);
        Assert.Equal("secret-1", key.KeyId);
        Assert.Equal(SecretKeyUses.Signature, key.Metadata.Use);
        Assert.Equal("RS256", key.Metadata.Algorithm);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenADecryptionRow_ReturnsAnEncryptionKey()
    {
        SetupGetPublicKey(OaepRow, CreateRsaSubjectPublicKeyInfo());

        var key = await Resolver.ReadKeyAsync(
            CreateBinding(OaepRow),
            new KeyMetadata { KeyId = "secret-1" },
            CancellationToken.None
        );

        Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Equal(SecretKeyUses.Encryption, key.Metadata.Use);
        Assert.Equal("RSA-OAEP-256", key.Metadata.Algorithm);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenTheKeySizeDiffers_Throws()
    {
        SetupGetPublicKey(LargeRsaRow, CreateRsaSubjectPublicKeyInfo());
        var binding = CreateBinding(LargeRsaRow);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Resolver.ReadKeyAsync(binding, new KeyMetadata(), CancellationToken.None)
        );

        Assert.Contains("key size 4096", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'key-1'", exception.Message, StringComparison.Ordinal);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenTheCurveDiffers_Throws()
    {
        SetupGetPublicKey(OtherCurveEccRow, CreateEccSubjectPublicKeyInfo());
        var binding = CreateBinding(OtherCurveEccRow);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Resolver.ReadKeyAsync(binding, new KeyMetadata(), CancellationToken.None)
        );

        Assert.Contains("curve 'secp256k1'", exception.Message, StringComparison.Ordinal);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenThePublicKeyIsNotValidDer_ThrowsWithTheReadFailure()
    {
        SetupGetPublicKey(RsaRow, [1, 2, 3]);
        var binding = CreateBinding(RsaRow);

        var exception = await Assert.ThrowsAsync<RemoteKeyRefusedException>(async () =>
            await Resolver.ReadKeyAsync(binding, new KeyMetadata(), CancellationToken.None)
        );

        Assert.Contains("'key-1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'rsa' key", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task ReadKeyAsync_WhenTheProviderRefusesTheKey_PropagatesItsException()
    {
        SetupGetPublicKeyCall(RsaRow)
            .ThrowsAsync(new CryptographicException("refused"))
            .Verifiable();
        var binding = CreateBinding(RsaRow);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Resolver.ReadKeyAsync(binding, new KeyMetadata(), CancellationToken.None)
        );
    }

    #endregion

    #region CreateKey Tests

    [Fact]
    public void CreateKey_WithAStoredHalf_ReturnsARemoteKeyWithTheMetadataId()
    {
        var spki = CreateEccSubjectPublicKeyInfo();
        var binding = CreateBinding(EccRow);

        var key = Resolver.CreateKey(binding, new KeyMetadata { KeyId = "secret-1" }, spki);

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
        Assert.Equal(SecretKeyTypes.Ecc, key.KeyType);
        Assert.Equal("secret-1", key.KeyId);
        Assert.Equal(SecretKeyUses.Signature, key.Metadata.Use);
        Assert.Equal("ES256", key.Metadata.Algorithm);
    }

    #endregion
}
