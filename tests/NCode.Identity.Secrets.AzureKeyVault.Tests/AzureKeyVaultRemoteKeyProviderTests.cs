// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Moq;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;
using KvSignatureAlgorithm = Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests;

public class AzureKeyVaultRemoteKeyProviderTests : BaseTests
{
    private const string VaultUri = "https://contoso.vault.azure.net";

    private const string Version = "0123456789abcdef0123456789abcdef";

    private const string KeyVersionId = VaultUri + "/keys/ncode-key/" + Version;

    private const string OtherKeyVersionId =
        VaultUri + "/keys/other-key/fedcba9876543210fedcba9876543210";

    private static readonly RemoteSigningAlgorithm Rs256 =
        new AzureKeyVaultRemoteKeyDescriptors().Algorithms.Single(x =>
            x.Algorithm == "RS256" && x.KeySizeBits == 3072
        );

    private static readonly RemoteSigningAlgorithm Es256 =
        new AzureKeyVaultRemoteKeyDescriptors().Algorithms.Single(x => x.Algorithm == "ES256");

    private static readonly KvSignatureAlgorithm Rs256Algorithm = new("RS256");

    private Mock<IAzureKeyVaultClientFactory> ClientFactory { get; }

    private Mock<KeyClient> KeyClient { get; }

    private Mock<CryptographyClient> CryptographyClient { get; }

    public AzureKeyVaultRemoteKeyProviderTests()
    {
        ClientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        KeyClient = CreateStrictMock<KeyClient>();
        CryptographyClient = CreateStrictMock<CryptographyClient>();
    }

    private AzureKeyVaultRemoteKeyProvider CreateProvider(
        string? vaultUri = VaultUri,
        bool hardwareProtected = true
    ) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new AzureKeyVaultRemoteKeyProviderOptions
                {
                    VaultUri = vaultUri,
                    HardwareProtected = hardwareProtected,
                }
            )
        );

    private static RemoteKeyCreationRequest CreateCreationRequest(RemoteSigningAlgorithm row) =>
        new()
        {
            OwnerId = "tenant-1",
            SecretId = "secret-1",
            Algorithm = row,
            GroupName = "ncid-group-1",
        };

    private static RemotePublicKeyRequest CreatePublicKeyRequest(
        RemoteSigningAlgorithm row,
        string keyId = KeyVersionId
    ) => new() { KeyId = keyId, Algorithm = row };

    private static string GetKeyVersionId(RemoteKeyCreationRequest request, string suffix) =>
        $"{VaultUri}/keys/{request.GroupName}{suffix}/{Version}";

    private static KeyVaultKey CreateKey(string id, JsonWebKey? webKey)
    {
        var uri = new Uri(id);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var properties = KeyModelFactory.KeyProperties(
            uri,
            new Uri(VaultUri),
            segments[1],
            segments.Length > 2 ? segments[2] : null,
            false,
            null,
            null,
            null
        );

        return KeyModelFactory.KeyVaultKey(properties, webKey);
    }

    private Response<KeyVaultKey> CreateResponse(KeyVaultKey key) =>
        Response.FromValue(key, CreateLooseMock<Response>().Object);

    // The factory runs the operation of the provider with the key client mock, as it runs it with the client of the
    // vault.
    private void SetupKeyClient<TResult>(string vaultUri) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteKeyClientAsync(
                    new Uri(vaultUri),
                    It.IsAny<Func<KeyClient, CancellationToken, ValueTask<TResult>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    Uri _,
                    Func<KeyClient, CancellationToken, ValueTask<TResult>> operation,
                    CancellationToken token
                ) => operation(KeyClient.Object, token)
            )
            .Verifiable();

    // The factory runs the key creation of the provider with the key client mock, as it runs it with the client of the
    // vault that never retries.
    private void SetupKeyClientWithoutRetry<TResult>(string vaultUri) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteKeyClientWithoutRetryAsync(
                    new Uri(vaultUri),
                    It.IsAny<Func<KeyClient, CancellationToken, ValueTask<TResult>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    Uri _,
                    Func<KeyClient, CancellationToken, ValueTask<TResult>> operation,
                    CancellationToken token
                ) => operation(KeyClient.Object, token)
            )
            .Verifiable();

    // The factory runs the operation of the provider with the cryptography client mock, as it runs it with the client
    // of the key.
    private void SetupCryptographyClient<TResult>(string keyId) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    keyId,
                    (KeyAccess?)null,
                    It.IsAny<Func<CryptographyClient, CancellationToken, ValueTask<TResult>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    KeyAccess? _,
                    Func<CryptographyClient, CancellationToken, ValueTask<TResult>> operation,
                    CancellationToken token
                ) => operation(CryptographyClient.Object, token)
            )
            .Verifiable();

    private void SetupCreateRsaKey(KeyVaultKey key, Action<CreateRsaKeyOptions> capture) =>
        KeyClient
            .Setup(x =>
                x.CreateRsaKeyAsync(It.IsAny<CreateRsaKeyOptions>(), It.IsAny<CancellationToken>())
            )
            .Callback<CreateRsaKeyOptions, CancellationToken>((options, _) => capture(options))
            .ReturnsAsync(CreateResponse(key))
            .Verifiable();

    private void SetupCreateEcKey(KeyVaultKey key, Action<CreateEcKeyOptions> capture) =>
        KeyClient
            .Setup(x =>
                x.CreateEcKeyAsync(It.IsAny<CreateEcKeyOptions>(), It.IsAny<CancellationToken>())
            )
            .Callback<CreateEcKeyOptions, CancellationToken>((options, _) => capture(options))
            .ReturnsAsync(CreateResponse(key))
            .Verifiable();

    private void SetupGetKey(KeyVaultKey key) =>
        KeyClient
            .Setup(x => x.GetKeyAsync("ncode-key", Version, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(key))
            .Verifiable();

    private void SetupSign(SignResult result, Action<byte[]>? capture = null) =>
        CryptographyClient
            .Setup(x =>
                x.SignAsync(
                    It.Is<KvSignatureAlgorithm>(a => a == Rs256Algorithm),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<KvSignatureAlgorithm, byte[], CancellationToken>(
                (_, digest, _) => capture?.Invoke(digest)
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static void AssertOperationsAndTags(
        CreateKeyOptions options,
        RemoteKeyCreationRequest request
    )
    {
        Assert.Equal([KeyOperation.Sign, KeyOperation.Verify], options.KeyOperations);
        Assert.Equal(4, options.Tags.Count);
        Assert.Equal("tenant-1", options.Tags["ncode-identity-owner"]);
        Assert.Equal("secret-1", options.Tags["ncode-identity-secret"]);
        Assert.Equal(request.Algorithm.Algorithm, options.Tags["ncode-identity-algorithm"]);
        Assert.Equal(request.GroupName, options.Tags["ncode-identity-group"]);
    }

    #region CreateKeyAsync Tests

    [Fact]
    public async Task CreateKeyAsync_RsaRow_SendsHardwareOptionsAndReturnsVersionId()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateCreationRequest(Rs256);
        var keyId = GetKeyVersionId(request, "-h");
        CreateRsaKeyOptions? sent = null;
        SetupKeyClientWithoutRetry<Response<KeyVaultKey>>(VaultUri);
        SetupCreateRsaKey(
            CreateKey(keyId, new JsonWebKey(rsa, false, null)),
            captured => sent = captured
        );
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(keyId, result.KeyId);
        var options = Assert.IsType<CreateRsaKeyOptions>(sent);
        Assert.Equal($"{request.GroupName}-h", options.Name);
        Assert.True(options.HardwareProtected);
        Assert.Equal(3072, options.KeySize);
        AssertOperationsAndTags(options, request);
    }

    [Fact]
    public async Task CreateKeyAsync_RsaRowSoftwareProtected_SendsSoftwareName()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateCreationRequest(Rs256);
        var keyId = GetKeyVersionId(request, "-s");
        CreateRsaKeyOptions? sent = null;
        SetupKeyClientWithoutRetry<Response<KeyVaultKey>>(VaultUri);
        SetupCreateRsaKey(
            CreateKey(keyId, new JsonWebKey(rsa, false, null)),
            captured => sent = captured
        );
        var provider = CreateProvider(hardwareProtected: false);

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(keyId, result.KeyId);
        var options = Assert.IsType<CreateRsaKeyOptions>(sent);
        Assert.Equal($"{request.GroupName}-s", options.Name);
        Assert.False(options.HardwareProtected);
    }

    [Fact]
    public async Task CreateKeyAsync_EllipticCurveRow_SendsCurveAndReturnsVersionId()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = CreateCreationRequest(Es256);
        var keyId = GetKeyVersionId(request, "-h");
        CreateEcKeyOptions? sent = null;
        SetupKeyClientWithoutRetry<Response<KeyVaultKey>>(VaultUri);
        SetupCreateEcKey(
            CreateKey(keyId, new JsonWebKey(ecdsa, false, null)),
            captured => sent = captured
        );
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(keyId, result.KeyId);
        var options = Assert.IsType<CreateEcKeyOptions>(sent);
        Assert.Equal($"{request.GroupName}-h", options.Name);
        Assert.True(options.HardwareProtected);
        Assert.Equal(KeyCurveName.P256, options.CurveName);
        AssertOperationsAndTags(options, request);
    }

    [Fact]
    public async Task CreateKeyAsync_ResultWithoutVersion_Throws()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateCreationRequest(Rs256);
        SetupKeyClientWithoutRetry<Response<KeyVaultKey>>(VaultUri);
        SetupCreateRsaKey(
            CreateKey($"{VaultUri}/keys/{request.GroupName}-h", new JsonWebKey(rsa, false, null)),
            _ => { }
        );
        var provider = CreateProvider();

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(request, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task CreateKeyAsync_VaultUriUnset_Throws()
    {
        var provider = CreateProvider(vaultUri: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task CreateKeyAsync_SecretTypeOfAnotherFamily_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .CreateKeyAsync(
                    CreateCreationRequest(Rs256 with { SecretType = "okp" }),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task CreateKeyAsync_EllipticCurveRowWithoutCurve_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .CreateKeyAsync(
                    CreateCreationRequest(Es256 with { Curve = null }),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    #endregion

    #region GetPublicKeyAsync Tests

    [Fact]
    public async Task GetPublicKeyAsync_RsaKey_WritesTheSubjectPublicKeyInfo()
    {
        using var rsa = RSA.Create(2048);
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupGetKey(CreateKey(KeyVersionId, new JsonWebKey(rsa, false, null)));
        var provider = CreateProvider();
        var writer = new ArrayBufferWriter<byte>();

        await provider.GetPublicKeyAsync(
            CreatePublicKeyRequest(Rs256),
            writer,
            CancellationToken.None
        );

        Assert.Equal(rsa.ExportSubjectPublicKeyInfo(), writer.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task GetPublicKeyAsync_EllipticCurveKey_WritesTheSubjectPublicKeyInfo()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupGetKey(CreateKey(KeyVersionId, new JsonWebKey(ecdsa, false, null)));
        var provider = CreateProvider();
        var writer = new ArrayBufferWriter<byte>();

        await provider.GetPublicKeyAsync(
            CreatePublicKeyRequest(Es256),
            writer,
            CancellationToken.None
        );

        Assert.Equal(ecdsa.ExportSubjectPublicKeyInfo(), writer.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task GetPublicKeyAsync_KeyIdWithoutVersion_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(Rs256, VaultUri + "/keys/ncode-key"),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task GetPublicKeyAsync_ResponseNamesAnotherKey_ThrowsIOException()
    {
        using var rsa = RSA.Create(2048);
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupGetKey(CreateKey(OtherKeyVersionId, new JsonWebKey(rsa, false, null)));
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(Rs256),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyVersionId, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_ResponseWithoutPublicKey_Throws()
    {
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupGetKey(CreateKey(KeyVersionId, null));
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(Rs256),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyVersionId, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenTheKeyIsNotRsaOrEc_Throws()
    {
        using var aes = Aes.Create();
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupGetKey(CreateKey(KeyVersionId, new JsonWebKey(aes)));
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(Rs256),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyVersionId, exception.Message);
    }

    #endregion

    #region SignAsync Tests

    [Fact]
    public async Task SignAsync_SendsAlgorithmAndDigestAndWritesSignature()
    {
        var digest = RandomNumberGenerator.GetBytes(32);
        var signature = RandomNumberGenerator.GetBytes(384);
        byte[]? sentDigest = null;
        SetupCryptographyClient<SignResult>(KeyVersionId);
        SetupSign(
            CryptographyModelFactory.SignResult(KeyVersionId, signature, Rs256Algorithm),
            sent => sentDigest = sent
        );
        var provider = CreateProvider();
        var buffer = new byte[512];

        var written = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = KeyVersionId,
                Algorithm = Rs256,
                Data = digest,
            },
            buffer,
            CancellationToken.None
        );

        Assert.Equal(signature, buffer[..written]);
        Assert.Equal(digest, sentDigest);
    }

    [Fact]
    public async Task SignAsync_SignatureLongerThanTheBuffer_Throws()
    {
        SetupCryptographyClient<SignResult>(KeyVersionId);
        SetupSign(
            CryptographyModelFactory.SignResult(
                KeyVersionId,
                RandomNumberGenerator.GetBytes(384),
                Rs256Algorithm
            )
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyVersionId,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[383],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyVersionId, exception.Message);
    }

    [Fact]
    public async Task SignAsync_ResultNamesAnotherKey_ThrowsIOException()
    {
        SetupCryptographyClient<SignResult>(KeyVersionId);
        SetupSign(
            CryptographyModelFactory.SignResult(OtherKeyVersionId, [1, 2, 3], Rs256Algorithm)
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyVersionId,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyVersionId, exception.Message);
    }

    [Fact]
    public async Task SignAsync_ResultNamesAnotherAlgorithm_ThrowsIOException()
    {
        SetupCryptographyClient<SignResult>(KeyVersionId);
        SetupSign(
            CryptographyModelFactory.SignResult(
                KeyVersionId,
                [1, 2, 3],
                new KvSignatureAlgorithm("PS256")
            )
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyVersionId,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(Rs256.SigningAlgorithm, exception.Message);
    }

    [Fact]
    public async Task SignAsync_KeyIdWithoutVersion_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = VaultUri + "/keys/ncode-key",
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task SignAsync_RowWithoutDigest_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyVersionId,
                        Algorithm = Rs256 with { DigestAlgorithm = null },
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    #endregion

    #region DestroyKeyAsync Tests

    private void SetupUpdateKeyProperties(
        Action<KeyProperties>? capture = null,
        Exception? exception = null
    )
    {
        var setup = KeyClient.Setup(x =>
            x.UpdateKeyPropertiesAsync(
                It.IsAny<KeyProperties>(),
                It.IsAny<IEnumerable<KeyOperation>>(),
                It.IsAny<CancellationToken>()
            )
        );

        setup.Callback<KeyProperties, IEnumerable<KeyOperation>, CancellationToken>(
            (properties, _, _) => capture?.Invoke(properties)
        );

        if (exception is not null)
        {
            setup.ThrowsAsync(exception).Verifiable();
            return;
        }

        setup.ReturnsAsync(CreateResponse(CreateKey(KeyVersionId, null))).Verifiable();
    }

    [Fact]
    public async Task DestroyKeyAsync_DisablesTheVersionInTheVaultOfTheKey()
    {
        KeyProperties? sent = null;
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupUpdateKeyProperties(captured => sent = captured);
        var provider = CreateProvider(vaultUri: "https://other.vault.azure.net");

        await provider.DestroyKeyAsync(KeyVersionId, CancellationToken.None);

        var properties = Assert.IsType<KeyProperties>(sent);
        Assert.False(properties.Enabled);
        Assert.Equal("ncode-key", properties.Name);
        Assert.Equal(Version, properties.Version);
    }

    [Fact]
    public async Task DestroyKeyAsync_VersionNotFound_Completes()
    {
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupUpdateKeyProperties(exception: new RequestFailedException(404, "Not found."));
        var provider = CreateProvider();

        await provider.DestroyKeyAsync(KeyVersionId, CancellationToken.None);
    }

    [Fact]
    public async Task DestroyKeyAsync_OtherFailure_Propagates()
    {
        var failure = new RequestFailedException(503, "Unavailable.");
        SetupKeyClient<Response<KeyVaultKey>>(VaultUri);
        SetupUpdateKeyProperties(exception: failure);
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            provider.DestroyKeyAsync(KeyVersionId, CancellationToken.None).AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyIdWithoutVersion_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.DestroyKeyAsync(VaultUri + "/keys/ncode-key", CancellationToken.None).AsTask()
        );

        Assert.Equal("keyId", exception.ParamName);
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_IsTheAzureKeyVaultProviderName()
    {
        var provider = CreateProvider();

        Assert.Equal(RemoteKeyProviderNames.AzureKeyVault, provider.Name);
    }

    #endregion
}
