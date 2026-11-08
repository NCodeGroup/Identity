// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Google.Api.Gax.Grpc;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Buffers;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class GoogleCloudKmsRemoteKeyProviderTests : BaseTests
{
    private const string KeyRing =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity";

    private const string OtherCryptoKeyVersionName =
        KeyRing + "/cryptoKeys/other/cryptoKeyVersions/1";

    private const string SignKeyName = KeyRing + "/cryptoKeys/signing/cryptoKeyVersions/7";

    private static RemoteSigningAlgorithm Rs256 { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.GoogleCloudKms,
            Algorithm = "RS256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
            KeySpec = "RSA_SIGN_PKCS1_3072_SHA256",
            SigningAlgorithm = "RSA_SIGN_PKCS1_3072_SHA256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };

    private static RemoteSigningAlgorithm Es256 { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.GoogleCloudKms,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "EC_SIGN_P256_SHA256",
            SigningAlgorithm = "EC_SIGN_P256_SHA256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private static readonly string PublicKeyPem;

    private static readonly byte[] PublicKeySpki;

    static GoogleCloudKmsRemoteKeyProviderTests()
    {
        using var keyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublicKeyPem = keyPair.ExportSubjectPublicKeyInfoPem();
        PublicKeySpki = keyPair.ExportSubjectPublicKeyInfo();
    }

    private Mock<IGoogleCloudKmsClientFactory> ClientFactory { get; }

    private Mock<KeyManagementServiceClient> Client { get; }

    private FakeTimeProvider TimeProvider { get; } = new();

    private int VersionGets { get; set; }

    public GoogleCloudKmsRemoteKeyProviderTests()
    {
        ClientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        Client = CreateStrictMock<KeyManagementServiceClient>();
    }

    private GoogleCloudKmsRemoteKeyProvider CreateProvider(
        string? keyRing = KeyRing,
        bool hardwareProtected = true
    ) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new GoogleCloudKmsRemoteKeyProviderOptions
                {
                    KeyRing = keyRing,
                    HardwareProtected = hardwareProtected,
                }
            ),
            TimeProvider
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
        string keyId
    ) => new() { KeyId = keyId, Algorithm = row };

    private static string CryptoKeyName(RemoteKeyCreationRequest request, char suffix = 'h') =>
        $"{KeyRing}/cryptoKeys/{request.GroupName}-{suffix}";

    private static string VersionName(RemoteKeyCreationRequest request, char suffix = 'h') =>
        $"{CryptoKeyName(request, suffix)}/cryptoKeyVersions/1";

    private static RpcException CreateRpcException(StatusCode statusCode) =>
        new(new Status(statusCode, "x"));

    private static void AssertSettings(
        CallSettings? settings,
        CallSettings expected,
        CancellationToken cancellationToken
    )
    {
        var actual = Assert.IsType<CallSettings>(settings);
        Assert.Same(expected.Retry, actual.Retry);
        Assert.Equal(cancellationToken, actual.CancellationToken);
    }

    // The factory runs the operation of the provider with the client mock, as it runs it with the deployment's client.
    private void SetupClient<TResult>() =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    (KeyAccess?)null,
                    It.IsAny<
                        Func<KeyManagementServiceClient, CancellationToken, ValueTask<TResult>>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    KeyAccess? _,
                    Func<
                        KeyManagementServiceClient,
                        CancellationToken,
                        ValueTask<TResult>
                    > operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupCreateVersion(
        Func<int, Task<CryptoKeyVersion>> respond,
        Action<CreateCryptoKeyVersionRequest, CallSettings?>? capture = null
    )
    {
        var calls = 0;
        Client
            .Setup(x =>
                x.CreateCryptoKeyVersionAsync(
                    It.IsAny<CreateCryptoKeyVersionRequest>(),
                    It.IsAny<CallSettings>()
                )
            )
            .Callback<CreateCryptoKeyVersionRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(() => respond(calls++))
            .Verifiable();
    }

    private void SetupCreateCryptoKey(
        Func<Task<CryptoKey>> respond,
        Action<CreateCryptoKeyRequest, CallSettings?>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.CreateCryptoKeyAsync(It.IsAny<CreateCryptoKeyRequest>(), It.IsAny<CallSettings>())
            )
            .Callback<CreateCryptoKeyRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(respond)
            .Verifiable();

    private void SetupGetVersion(
        Func<int, CryptoKeyVersion.Types.CryptoKeyVersionState> stateAt,
        Action<GetCryptoKeyVersionRequest, CallSettings?>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.GetCryptoKeyVersionAsync(
                    It.IsAny<GetCryptoKeyVersionRequest>(),
                    It.IsAny<CallSettings>()
                )
            )
            .Callback<GetCryptoKeyVersionRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(
                (GetCryptoKeyVersionRequest request, CallSettings? _) =>
                    Task.FromResult(
                        new CryptoKeyVersion { Name = request.Name, State = stateAt(VersionGets++) }
                    )
            )
            .Verifiable();

    private void SetupGetPublicKey(
        PublicKey response,
        Action<GetPublicKeyRequest, CallSettings?>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.GetPublicKeyAsync(It.IsAny<GetPublicKeyRequest>(), It.IsAny<CallSettings>())
            )
            .Callback<GetPublicKeyRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(Task.FromResult(response))
            .Verifiable();

    private static PublicKey CreatePublicKey(
        string versionName,
        string? pem = null,
        long? pemCrc32C = null,
        CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm? algorithm = null,
        string? name = null
    )
    {
        pem ??= PublicKeyPem;
        return new PublicKey
        {
            Name = name ?? versionName,
            Pem = pem,
            PemCrc32C =
                pemCrc32C ?? GoogleCloudKmsChecksum.Compute(SecureEncoding.UTF8.GetBytes(pem)),
            Algorithm =
                algorithm ?? CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm.EcSignP256Sha256,
        };
    }

    // The provider waits on the fake time, so the test moves it forward until the call completes.
    private async Task AdvanceUntilCompletedAsync(Task task)
    {
        for (var i = 0; i < 500 && !task.IsCompleted; ++i)
        {
            TimeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); ++i)
        {
            await Task.Yield();
        }

        Assert.True(condition());
    }

    private void SetupReadOfAnEnabledVersion(PublicKey publicKey)
    {
        SetupClient<CryptoKeyVersion>();
        SetupClient<PublicKey>();
        SetupGetVersion(_ => CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled);
        SetupGetPublicKey(publicKey);
    }

    private static async Task<byte[]> ReadPublicKeyAsync(
        GoogleCloudKmsRemoteKeyProvider provider,
        RemotePublicKeyRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var writer = new ArrayBufferWriter<byte>();
        await provider.GetPublicKeyAsync(request, writer, cancellationToken);
        return writer.WrittenSpan.ToArray();
    }

    #region CreateKeyAsync Tests

    [Fact]
    public async Task CreateKeyAsync_ExistingCryptoKey_CreatesAVersionAndReturnsItsName()
    {
        var request = CreateCreationRequest(Es256);
        using var cancellationSource = new CancellationTokenSource();
        CreateCryptoKeyVersionRequest? sent = null;
        CallSettings? sentSettings = null;
        SetupClient<CryptoKeyVersion>();
        SetupCreateVersion(
            _ => Task.FromResult(new CryptoKeyVersion { Name = VersionName(request) }),
            (versionRequest, settings) =>
            {
                sent = versionRequest;
                sentSettings = settings;
            }
        );
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, cancellationSource.Token);

        Assert.Equal(VersionName(request), result.KeyId);
        var versionRequest = Assert.IsType<CreateCryptoKeyVersionRequest>(sent);
        Assert.Equal(CryptoKeyName(request), versionRequest.Parent);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Creating, cancellationSource.Token);
    }

    [Fact]
    public async Task CreateKeyAsync_SoftwareProtection_UsesTheSoftwareCryptoKey()
    {
        var request = CreateCreationRequest(Es256);
        CreateCryptoKeyVersionRequest? sent = null;
        SetupClient<CryptoKeyVersion>();
        SetupCreateVersion(
            _ => Task.FromResult(new CryptoKeyVersion { Name = VersionName(request, 's') }),
            (versionRequest, _) => sent = versionRequest
        );
        var provider = CreateProvider(hardwareProtected: false);

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(VersionName(request, 's'), result.KeyId);
        Assert.Equal(
            CryptoKeyName(request, 's'),
            Assert.IsType<CreateCryptoKeyVersionRequest>(sent).Parent
        );
    }

    [Theory]
    [InlineData(true, 'h', ProtectionLevel.Hsm)]
    [InlineData(false, 's', ProtectionLevel.Software)]
    public async Task CreateKeyAsync_CryptoKeyNotFound_CreatesTheCryptoKeyAndTheVersion(
        bool hardwareProtected,
        char suffix,
        ProtectionLevel protectionLevel
    )
    {
        var request = CreateCreationRequest(Es256);
        using var cancellationSource = new CancellationTokenSource();
        CreateCryptoKeyRequest? sentCryptoKey = null;
        CallSettings? sentSettings = null;
        SetupClient<CryptoKeyVersion>();
        SetupClient<CryptoKey>();
        SetupCreateVersion(call =>
            call == 0
                ? Task.FromException<CryptoKeyVersion>(CreateRpcException(StatusCode.NotFound))
                : Task.FromResult(new CryptoKeyVersion { Name = VersionName(request, suffix) })
        );
        SetupCreateCryptoKey(
            () => Task.FromResult(new CryptoKey()),
            (cryptoKeyRequest, settings) =>
            {
                sentCryptoKey = cryptoKeyRequest;
                sentSettings = settings;
            }
        );
        var provider = CreateProvider(hardwareProtected: hardwareProtected);

        var result = await provider.CreateKeyAsync(request, cancellationSource.Token);

        Assert.Equal(VersionName(request, suffix), result.KeyId);
        var cryptoKeyRequest = Assert.IsType<CreateCryptoKeyRequest>(sentCryptoKey);
        Assert.Equal(KeyRing, cryptoKeyRequest.Parent);
        Assert.Equal($"{request.GroupName}-{suffix}", cryptoKeyRequest.CryptoKeyId);
        Assert.True(cryptoKeyRequest.SkipInitialVersionCreation);
        var cryptoKey = cryptoKeyRequest.CryptoKey;
        Assert.Equal(CryptoKey.Types.CryptoKeyPurpose.AsymmetricSign, cryptoKey.Purpose);
        Assert.Equal(
            CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm.EcSignP256Sha256,
            cryptoKey.VersionTemplate.Algorithm
        );
        Assert.Equal(protectionLevel, cryptoKey.VersionTemplate.ProtectionLevel);
        Assert.Equal(2, cryptoKey.Labels.Count);
        Assert.Equal(request.GroupName, cryptoKey.Labels["ncode-identity-group"]);
        Assert.Equal("es256", cryptoKey.Labels["ncode-identity-algorithm"]);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Creating, cancellationSource.Token);
    }

    [Fact]
    public async Task CreateKeyAsync_CryptoKeyAlreadyExists_Continues()
    {
        var request = CreateCreationRequest(Es256);
        SetupClient<CryptoKeyVersion>();
        SetupClient<CryptoKey>();
        SetupCreateVersion(call =>
            call == 0
                ? Task.FromException<CryptoKeyVersion>(CreateRpcException(StatusCode.NotFound))
                : Task.FromResult(new CryptoKeyVersion { Name = VersionName(request) })
        );
        SetupCreateCryptoKey(() =>
            Task.FromException<CryptoKey>(CreateRpcException(StatusCode.AlreadyExists))
        );
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(VersionName(request), result.KeyId);
    }

    [Fact]
    public async Task CreateKeyAsync_CryptoKeyCreationFails_Propagates()
    {
        var request = CreateCreationRequest(Es256);
        SetupClient<CryptoKeyVersion>();
        SetupClient<CryptoKey>();
        SetupCreateVersion(_ =>
            Task.FromException<CryptoKeyVersion>(CreateRpcException(StatusCode.NotFound))
        );
        SetupCreateCryptoKey(() =>
            Task.FromException<CryptoKey>(CreateRpcException(StatusCode.PermissionDenied))
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            provider.CreateKeyAsync(request, CancellationToken.None).AsTask()
        );

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
    }

    [Fact]
    public async Task CreateKeyAsync_VersionCreationFails_Propagates()
    {
        var request = CreateCreationRequest(Es256);
        SetupClient<CryptoKeyVersion>();
        SetupCreateVersion(_ =>
            Task.FromException<CryptoKeyVersion>(CreateRpcException(StatusCode.PermissionDenied))
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            provider.CreateKeyAsync(request, CancellationToken.None).AsTask()
        );

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("projects/my-project/locations/us-central1")]
    public async Task CreateKeyAsync_InvalidKeyRing_Throws(string? keyRing)
    {
        var provider = CreateProvider(keyRing: keyRing);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Es256), CancellationToken.None).AsTask()
        );

        Assert.Contains("KeyRing", exception.Message);
    }

    [Fact]
    public async Task CreateKeyAsync_UnknownKeySpec_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .CreateKeyAsync(
                    CreateCreationRequest(Es256 with { KeySpec = "NOT_AN_ALGORITHM" }),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task CreateKeyAsync_VersionOfAnotherCryptoKey_Throws()
    {
        SetupClient<CryptoKeyVersion>();
        SetupCreateVersion(_ =>
            Task.FromResult(new CryptoKeyVersion { Name = OtherCryptoKeyVersionName })
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Es256), CancellationToken.None).AsTask()
        );

        Assert.Contains(OtherCryptoKeyVersionName, exception.Message);
    }

    #endregion

    #region GetPublicKeyAsync Tests

    [Fact]
    public async Task GetPublicKeyAsync_EnabledVersion_WritesTheSubjectPublicKeyInfo()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupReadOfAnEnabledVersion(CreatePublicKey(versionName));
        var provider = CreateProvider();

        var written = await ReadPublicKeyAsync(
            provider,
            CreatePublicKeyRequest(Es256, versionName)
        );

        Assert.Equal(PublicKeySpki, written);
    }

    [Fact]
    public async Task GetPublicKeyAsync_VersionPendingGeneration_PollsWithADoublingDelayUntilEnabled()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        CallSettings? sentSettings = null;
        SetupClient<CryptoKeyVersion>();
        SetupClient<PublicKey>();
        SetupGetVersion(
            call =>
                call < 3
                    ? CryptoKeyVersion.Types.CryptoKeyVersionState.PendingGeneration
                    : CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled,
            (_, settings) => sentSettings = settings
        );
        SetupGetPublicKey(CreatePublicKey(versionName));
        var provider = CreateProvider();

        var task = ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName));

        await WaitUntilAsync(() => VersionGets == 1);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(249));
        await Task.Yield();
        Assert.Equal(1, VersionGets);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => VersionGets == 2);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(499));
        await Task.Yield();
        Assert.Equal(2, VersionGets);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => VersionGets == 3);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Yield();
        Assert.Equal(3, VersionGets);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(1));
        var written = await task;

        Assert.Equal(4, VersionGets);
        Assert.Equal(PublicKeySpki, written);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Retrying, CancellationToken.None);
    }

    [Fact]
    public async Task GetPublicKeyAsync_GenerationFailed_Throws()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupClient<CryptoKeyVersion>();
        SetupGetVersion(_ => CryptoKeyVersion.Types.CryptoKeyVersionState.GenerationFailed);
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName))
        );

        Assert.Contains(versionName, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_VersionNeverEnabled_ThrowsAtTheBound()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupClient<CryptoKeyVersion>();
        SetupGetVersion(_ => CryptoKeyVersion.Types.CryptoKeyVersionState.PendingGeneration);
        var provider = CreateProvider();
        var started = TimeProvider.GetUtcNow();

        var task = ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName));
        await AdvanceUntilCompletedAsync(task);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains(versionName, exception.Message);
        Assert.Equal(20, VersionGets);
        Assert.True(TimeProvider.GetUtcNow() - started >= TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKey_IsReadWithTheRetryingSettings()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        using var cancellationSource = new CancellationTokenSource();
        GetPublicKeyRequest? sent = null;
        CallSettings? sentSettings = null;
        SetupClient<CryptoKeyVersion>();
        SetupClient<PublicKey>();
        SetupGetVersion(_ => CryptoKeyVersion.Types.CryptoKeyVersionState.Enabled);
        SetupGetPublicKey(
            CreatePublicKey(versionName),
            (publicKeyRequest, settings) =>
            {
                sent = publicKeyRequest;
                sentSettings = settings;
            }
        );
        var provider = CreateProvider();

        await ReadPublicKeyAsync(
            provider,
            CreatePublicKeyRequest(Es256, versionName),
            cancellationSource.Token
        );

        Assert.Equal(versionName, Assert.IsType<GetPublicKeyRequest>(sent).Name);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Retrying, cancellationSource.Token);
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyNamesAnotherVersion_Throws()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupReadOfAnEnabledVersion(CreatePublicKey(versionName, name: OtherCryptoKeyVersionName));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<IOException>(() =>
            ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName))
        );
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyFailsItsChecksum_Throws()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupReadOfAnEnabledVersion(CreatePublicKey(versionName, pemCrc32C: 1));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<IOException>(() =>
            ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName))
        );
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyOfAnotherAlgorithm_Throws()
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupReadOfAnEnabledVersion(
            CreatePublicKey(
                versionName,
                algorithm: CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm.EcSignP384Sha384
            )
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName))
        );

        Assert.Contains(versionName, exception.Message);
    }

    [Theory]
    [InlineData("not a pem")]
    [InlineData("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n")]
    [InlineData("-----BEGIN PUBLIC KEY-----\n!!!!\n-----END PUBLIC KEY-----\n")]
    public async Task GetPublicKeyAsync_PublicKeyIsNotAPemSubjectPublicKeyInfo_Throws(string pem)
    {
        var versionName = VersionName(CreateCreationRequest(Es256));
        SetupReadOfAnEnabledVersion(CreatePublicKey(versionName, pem: pem));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<CryptographicException>(() =>
            ReadPublicKeyAsync(provider, CreatePublicKeyRequest(Es256, versionName))
        );
    }

    [Fact]
    public async Task GetPublicKeyAsync_KeyIdIsNotAVersionName_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReadPublicKeyAsync(
                provider,
                CreatePublicKeyRequest(Es256, KeyRing + "/cryptoKeys/signing")
            )
        );

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task GetPublicKeyAsync_UnknownKeySpec_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            ReadPublicKeyAsync(
                provider,
                CreatePublicKeyRequest(Es256 with { KeySpec = "NOT_AN_ALGORITHM" }, SignKeyName)
            )
        );

        Assert.Equal("request", exception.ParamName);
    }

    #endregion

    #region SignAsync Tests

    private void SetupSign(
        AsymmetricSignResponse response,
        Action<AsymmetricSignRequest, CallSettings?>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.AsymmetricSignAsync(It.IsAny<AsymmetricSignRequest>(), It.IsAny<CallSettings>())
            )
            .Callback<AsymmetricSignRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(Task.FromResult(response))
            .Verifiable();

    private static AsymmetricSignResponse CreateDigestSignResponse(
        byte[] signature,
        string name = SignKeyName,
        bool verifiedDigest = true,
        long? signatureCrc32C = null
    ) =>
        new()
        {
            Name = name,
            Signature = ByteString.CopyFrom(signature),
            SignatureCrc32C = signatureCrc32C ?? GoogleCloudKmsChecksum.Compute(signature),
            VerifiedDigestCrc32C = verifiedDigest,
        };

    [Theory]
    [InlineData("SHA256", Digest.DigestOneofCase.Sha256, 32)]
    [InlineData("SHA384", Digest.DigestOneofCase.Sha384, 48)]
    [InlineData("SHA512", Digest.DigestOneofCase.Sha512, 64)]
    public async Task SignAsync_DigestRow_SendsTheDigestInItsFieldWithItsChecksum(
        string hashName,
        Digest.DigestOneofCase digestCase,
        int digestLength
    )
    {
        var row = Rs256 with { DigestAlgorithm = new HashAlgorithmName(hashName) };
        var digest = RandomNumberGenerator.GetBytes(digestLength);
        var signature = RandomNumberGenerator.GetBytes(384);
        using var cancellationSource = new CancellationTokenSource();
        AsymmetricSignRequest? sent = null;
        CallSettings? sentSettings = null;
        SetupClient<AsymmetricSignResponse>();
        SetupSign(
            CreateDigestSignResponse(signature),
            (request, settings) =>
            {
                sent = request;
                sentSettings = settings;
            }
        );
        var provider = CreateProvider();
        var buffer = new byte[512];

        var written = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = SignKeyName,
                Algorithm = row,
                Data = digest,
            },
            buffer,
            cancellationSource.Token
        );

        Assert.Equal(signature, buffer[..written]);
        var request = Assert.IsType<AsymmetricSignRequest>(sent);
        Assert.Equal(SignKeyName, request.Name);
        Assert.Equal(digestCase, request.Digest.DigestCase);
        Assert.Equal(GoogleCloudKmsChecksum.Compute(digest), request.DigestCrc32C);
        Assert.Equal(0, request.Data.Length);
        Assert.Null(request.DataCrc32C);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Retrying, cancellationSource.Token);
    }

    [Fact]
    public async Task SignAsync_DigestRow_SendsTheDigestBytes()
    {
        var digest = RandomNumberGenerator.GetBytes(32);
        byte[]? sentDigest = null;
        SetupClient<AsymmetricSignResponse>();
        SetupSign(
            CreateDigestSignResponse([1, 2, 3]),
            (request, _) => sentDigest = request.Digest.Sha256.ToByteArray()
        );
        var provider = CreateProvider();

        await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = SignKeyName,
                Algorithm = Rs256,
                Data = digest,
            },
            new byte[16],
            CancellationToken.None
        );

        Assert.Equal(digest, sentDigest);
    }

    [Fact]
    public async Task SignAsync_SignatureLongerThanTheBuffer_Throws()
    {
        SetupClient<AsymmetricSignResponse>();
        SetupSign(CreateDigestSignResponse([1, 2, 3, 4]));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[3],
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task SignAsync_RowWithoutDigest_SendsTheDataWithItsChecksum()
    {
        var row = Rs256 with { DigestAlgorithm = null };
        var input = "signing input"u8.ToArray();
        var signature = RandomNumberGenerator.GetBytes(384);
        AsymmetricSignRequest? sent = null;
        byte[]? sentData = null;
        SetupClient<AsymmetricSignResponse>();
        SetupSign(
            new AsymmetricSignResponse
            {
                Name = SignKeyName,
                Signature = ByteString.CopyFrom(signature),
                SignatureCrc32C = GoogleCloudKmsChecksum.Compute(signature),
                VerifiedDataCrc32C = true,
            },
            (request, _) =>
            {
                sent = request;
                sentData = request.Data.ToByteArray();
            }
        );
        var provider = CreateProvider();
        var buffer = new byte[512];

        var written = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = SignKeyName,
                Algorithm = row,
                Data = input,
            },
            buffer,
            CancellationToken.None
        );

        Assert.Equal(signature, buffer[..written]);
        var request = Assert.IsType<AsymmetricSignRequest>(sent);
        Assert.Equal(input, sentData);
        Assert.Equal(GoogleCloudKmsChecksum.Compute(input), request.DataCrc32C);
        Assert.Null(request.Digest);
        Assert.Null(request.DigestCrc32C);
    }

    [Fact]
    public async Task SignAsync_RequestChecksumNotVerified_Throws()
    {
        SetupClient<AsymmetricSignResponse>();
        SetupSign(CreateDigestSignResponse([1, 2, 3], verifiedDigest: false));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task SignAsync_DataChecksumNotVerified_Throws()
    {
        var row = Rs256 with { DigestAlgorithm = null };
        SetupClient<AsymmetricSignResponse>();
        SetupSign(
            new AsymmetricSignResponse
            {
                Name = SignKeyName,
                Signature = ByteString.CopyFrom([1, 2, 3]),
                SignatureCrc32C = GoogleCloudKmsChecksum.Compute([1, 2, 3]),
                VerifiedDigestCrc32C = true,
                VerifiedDataCrc32C = false,
            }
        );
        var provider = CreateProvider();

        await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = row,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task SignAsync_SignatureFailsItsChecksum_Throws()
    {
        SetupClient<AsymmetricSignResponse>();
        SetupSign(CreateDigestSignResponse([1, 2, 3], signatureCrc32C: 1));
        var provider = CreateProvider();

        await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task SignAsync_ResponseNamesAnotherVersion_Throws()
    {
        SetupClient<AsymmetricSignResponse>();
        SetupSign(CreateDigestSignResponse([1, 2, 3], name: OtherCryptoKeyVersionName));
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(SignKeyName, exception.Message);
    }

    [Fact]
    public async Task SignAsync_KeyIdIsNotAVersionName_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyRing + "/cryptoKeys/signing",
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
    public async Task SignAsync_DigestOfAnotherHashAlgorithm_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = SignKeyName,
                        Algorithm = Rs256 with { DigestAlgorithm = HashAlgorithmName.SHA1 },
                        Data = new byte[20],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("digestAlgorithm", exception.ParamName);
    }

    #endregion

    #region DestroyKeyAsync Tests

    private void SetupDestroy(
        Func<Task<CryptoKeyVersion>> respond,
        Action<DestroyCryptoKeyVersionRequest, CallSettings?>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.DestroyCryptoKeyVersionAsync(
                    It.IsAny<DestroyCryptoKeyVersionRequest>(),
                    It.IsAny<CallSettings>()
                )
            )
            .Callback<DestroyCryptoKeyVersionRequest, CallSettings?>(
                (request, settings) => capture?.Invoke(request, settings)
            )
            .Returns(respond)
            .Verifiable();

    [Fact]
    public async Task DestroyKeyAsync_Version_IsDestroyedWithTheRetryingSettings()
    {
        using var cancellationSource = new CancellationTokenSource();
        DestroyCryptoKeyVersionRequest? sent = null;
        CallSettings? sentSettings = null;
        SetupClient<CryptoKeyVersion>();
        SetupDestroy(
            () => Task.FromResult(new CryptoKeyVersion()),
            (request, settings) =>
            {
                sent = request;
                sentSettings = settings;
            }
        );
        var provider = CreateProvider();

        await provider.DestroyKeyAsync(SignKeyName, cancellationSource.Token);

        Assert.Equal(SignKeyName, Assert.IsType<DestroyCryptoKeyVersionRequest>(sent).Name);
        AssertSettings(sentSettings, GoogleCloudKmsCallSettings.Retrying, cancellationSource.Token);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.FailedPrecondition)]
    public async Task DestroyKeyAsync_VersionMissingOrAlreadyDestroyed_Completes(
        StatusCode statusCode
    )
    {
        SetupClient<CryptoKeyVersion>();
        SetupDestroy(() => Task.FromException<CryptoKeyVersion>(CreateRpcException(statusCode)));
        var provider = CreateProvider();

        await provider.DestroyKeyAsync(SignKeyName, CancellationToken.None);
    }

    [Fact]
    public async Task DestroyKeyAsync_OtherFailure_Propagates()
    {
        SetupClient<CryptoKeyVersion>();
        SetupDestroy(() =>
            Task.FromException<CryptoKeyVersion>(CreateRpcException(StatusCode.PermissionDenied))
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            provider.DestroyKeyAsync(SignKeyName, CancellationToken.None).AsTask()
        );

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyIdIsNotAVersionName_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.DestroyKeyAsync("not-a-version", CancellationToken.None).AsTask()
        );

        Assert.Equal("keyId", exception.ParamName);
    }

    #endregion

    #region TryGetAlgorithm Tests

    [Fact]
    public void TryGetAlgorithm_UnknownName_ReturnsFalse()
    {
        Assert.False(GoogleCloudKmsRemoteKeyProvider.TryGetAlgorithm("RSA_2048", out _));
    }

    [Fact]
    public void TryGetAlgorithm_WireName_ReturnsTheMember()
    {
        Assert.True(
            GoogleCloudKmsRemoteKeyProvider.TryGetAlgorithm(
                "RSA_SIGN_PSS_3072_SHA256",
                out var algorithm
            )
        );
        Assert.Equal(
            CryptoKeyVersion.Types.CryptoKeyVersionAlgorithm.RsaSignPss3072Sha256,
            algorithm
        );
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_IsGoogleCloudKms()
    {
        var provider = CreateProvider();

        Assert.Equal(RemoteKeyProviderNames.GoogleCloudKms, provider.Name);
    }

    #endregion
}
