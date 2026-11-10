// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Moq;
using NCode.Identity.Secrets.AwsKms.Facades;
using NCode.Identity.Secrets.AwsKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests;

public class AwsKmsRemoteKeyProviderTests : BaseTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private const string OtherRegionKeyArn =
        "arn:aws:kms:eu-central-1:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private const string OtherKeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private static RemoteSigningAlgorithm Rs256 { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
            KeySpec = "RSA_3072",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };

    private static RemoteSigningAlgorithm Es256 { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private static RemoteDecryptionAlgorithm RsaOaep256 { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private static RemoteKeyAgreementAlgorithm EcdhEs { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ECDH-ES",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            KeyAgreementAlgorithm = "ECDH",
        };

    private Mock<IAwsKmsClientFactory> ClientFactory { get; }

    private Mock<IAmazonKeyManagementService> Client { get; }

    public AwsKmsRemoteKeyProviderTests()
    {
        ClientFactory = CreateStrictMock<IAwsKmsClientFactory>();
        Client = CreateStrictMock<IAmazonKeyManagementService>();
    }

    private AwsKmsRemoteKeyProvider CreateProvider(
        string? region = "us-west-2",
        int pendingWindowInDays = 30
    ) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new AwsKmsRemoteKeyProviderOptions
                {
                    Region = region,
                    PendingWindowInDays = pendingWindowInDays,
                }
            )
        );

    private static RemoteKeyCreationRequest CreateCreationRequest(RemoteKeyAlgorithm row) =>
        new()
        {
            OwnerId = "tenant-1",
            SecretId = "secret-1",
            Algorithm = row,
            GroupName = "ncid-group-1",
            Deployment = "deployment-1",
        };

    // The factory runs the operation of the provider with the client mock, as it runs it with a client of the region.
    private void SetupClient<TResult>(string region) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    region,
                    (KeyAccess?)null,
                    It.IsAny<
                        Func<IAmazonKeyManagementService, CancellationToken, ValueTask<TResult>>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    KeyAccess? _,
                    Func<
                        IAmazonKeyManagementService,
                        CancellationToken,
                        ValueTask<TResult>
                    > operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupClientWithoutRetry<TResult>(string region) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteWithoutRetryAsync(
                    region,
                    It.IsAny<
                        Func<IAmazonKeyManagementService, CancellationToken, ValueTask<TResult>>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    Func<
                        IAmazonKeyManagementService,
                        CancellationToken,
                        ValueTask<TResult>
                    > operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupCreateKey(string? arn, Action<CreateKeyRequest>? capture = null) =>
        Client
            .Setup(x =>
                x.CreateKeyAsync(It.IsAny<CreateKeyRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<CreateKeyRequest, CancellationToken>((request, _) => capture?.Invoke(request))
            .ReturnsAsync(new CreateKeyResponse { KeyMetadata = new KeyMetadata { Arn = arn } })
            .Verifiable();

    private void SetupGetPublicKey(GetPublicKeyResponse response, string expectedKeyId) =>
        Client
            .Setup(x =>
                x.GetPublicKeyAsync(
                    It.Is<GetPublicKeyRequest>(r => r.KeyId == expectedKeyId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response)
            .Verifiable();

    private static GetPublicKeyResponse CreatePublicKey(
        RemoteKeyAlgorithm row,
        byte[]? publicKey,
        string keyId = KeyArn,
        KeyUsageType? usage = null,
        string? keySpec = null,
        List<string>? signingAlgorithms = null,
        List<string>? encryptionAlgorithms = null,
        List<string>? keyAgreementAlgorithms = null
    ) =>
        row switch
        {
            RemoteDecryptionAlgorithm decryption => new GetPublicKeyResponse
            {
                KeyId = keyId,
                PublicKey = publicKey is null ? null : new MemoryStream(publicKey),
                KeyUsage = usage ?? KeyUsageType.ENCRYPT_DECRYPT,
                KeySpec = KeySpec.FindValue(keySpec ?? row.KeySpec),
                EncryptionAlgorithms = encryptionAlgorithms ?? [decryption.EncryptionAlgorithm],
            },
            RemoteKeyAgreementAlgorithm agreement => new GetPublicKeyResponse
            {
                KeyId = keyId,
                PublicKey = publicKey is null ? null : new MemoryStream(publicKey),
                KeyUsage = usage ?? KeyUsageType.KEY_AGREEMENT,
                KeySpec = KeySpec.FindValue(keySpec ?? row.KeySpec),
                KeyAgreementAlgorithms =
                    keyAgreementAlgorithms ?? [agreement.KeyAgreementAlgorithm],
            },
            RemoteSigningAlgorithm signing => new GetPublicKeyResponse
            {
                KeyId = keyId,
                PublicKey = publicKey is null ? null : new MemoryStream(publicKey),
                KeyUsage = usage ?? KeyUsageType.SIGN_VERIFY,
                KeySpec = KeySpec.FindValue(keySpec ?? row.KeySpec),
                SigningAlgorithms = signingAlgorithms ?? [signing.SigningAlgorithm],
            },
            _ => throw new ArgumentException("Unknown row kind.", nameof(row)),
        };

    private static RemotePublicKeyRequest CreatePublicKeyRequest(
        RemoteKeyAlgorithm row,
        string keyId = KeyArn
    ) => new() { KeyId = keyId, Algorithm = row };

    #region CreateKeyAsync Tests

    [Fact]
    public async Task CreateKeyAsync_SendsSpecUsageTagsAndDescription_ReturnsArn()
    {
        var request = CreateCreationRequest(Rs256);
        CreateKeyRequest? sent = null;
        SetupClientWithoutRetry<CreateKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(KeyArn, result.KeyId);
        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.RSA_3072, createRequest.KeySpec);
        Assert.Equal(KeyUsageType.SIGN_VERIFY, createRequest.KeyUsage);
        Assert.Equal("NCode.Identity key secret-1 of tenant-1", createRequest.Description);
        Assert.Equal(5, createRequest.Tags.Count);
        Assert.Equal("tenant-1", Tag(createRequest, "ncode-identity:owner"));
        Assert.Equal("secret-1", Tag(createRequest, "ncode-identity:secret"));
        Assert.Equal("RS256", Tag(createRequest, "ncode-identity:algorithm"));
        Assert.Equal(request.GroupName, Tag(createRequest, "ncode-identity:group"));
        Assert.Equal("deployment-1", Tag(createRequest, AwsKmsRemoteKeyProvider.DeploymentTagKey));
    }

    [Fact]
    public async Task CreateKeyAsync_EllipticCurveRow_SendsCurveSpec()
    {
        var request = CreateCreationRequest(Es256);
        CreateKeyRequest? sent = null;
        SetupClientWithoutRetry<CreateKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        var provider = CreateProvider();

        await provider.CreateKeyAsync(request, CancellationToken.None);

        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.ECC_NIST_P256, createRequest.KeySpec);
    }

    [Fact]
    public async Task CreateKeyAsync_DecryptionRow_SendsEncryptDecryptUsage()
    {
        var request = CreateCreationRequest(RsaOaep256);
        CreateKeyRequest? sent = null;
        SetupClientWithoutRetry<CreateKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        var provider = CreateProvider();

        await provider.CreateKeyAsync(request, CancellationToken.None);

        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.RSA_2048, createRequest.KeySpec);
        Assert.Equal(KeyUsageType.ENCRYPT_DECRYPT, createRequest.KeyUsage);
        Assert.Equal("RSA-OAEP-256", Tag(createRequest, "ncode-identity:algorithm"));
    }

    [Fact]
    public async Task CreateKeyAsync_KeyAgreementRow_SendsKeyAgreementUsage()
    {
        var request = CreateCreationRequest(EcdhEs);
        CreateKeyRequest? sent = null;
        SetupClientWithoutRetry<CreateKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        var provider = CreateProvider();

        await provider.CreateKeyAsync(request, CancellationToken.None);

        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.ECC_NIST_P256, createRequest.KeySpec);
        Assert.Equal(KeyUsageType.KEY_AGREEMENT, createRequest.KeyUsage);
        Assert.Equal("ECDH-ES", Tag(createRequest, "ncode-identity:algorithm"));
    }

    [Fact]
    public async Task CreateKeyAsync_RegionUnset_Throws()
    {
        var provider = CreateProvider(region: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task CreateKeyAsync_ServiceReturnsNoArn_Throws()
    {
        SetupClientWithoutRetry<CreateKeyResponse>("us-west-2");
        SetupCreateKey("not-an-arn");
        var provider = CreateProvider();

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );
    }

    private static string? Tag(CreateKeyRequest request, string key) =>
        request.Tags.Single(x => x.TagKey == key).TagValue;

    #endregion

    #region GetPublicKeyAsync Tests

    [Fact]
    public async Task GetPublicKeyAsync_WritesThePublicKeyUsingTheClientOfTheArnRegion()
    {
        var spki = RandomNumberGenerator.GetBytes(91);
        SetupClient<GetPublicKeyResponse>("eu-central-1");
        SetupGetPublicKey(CreatePublicKey(Es256, spki, OtherRegionKeyArn), OtherRegionKeyArn);
        var provider = CreateProvider(region: "us-west-2");
        var writer = new ArrayBufferWriter<byte>();

        await provider.GetPublicKeyAsync(
            CreatePublicKeyRequest(Es256, OtherRegionKeyArn),
            writer,
            CancellationToken.None
        );

        Assert.Equal(spki, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task GetPublicKeyAsync_KeyIdIsNotAnArn_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(Rs256, "not-an-arn"),
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
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(CreatePublicKey(Rs256, [1, 2, 3], OtherKeyArn), KeyArn);
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

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyOfAnotherSpec_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(CreatePublicKey(Rs256, [1, 2, 3], keySpec: "RSA_2048"), KeyArn);
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

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyOfAnotherUsage_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(Rs256, [1, 2, 3], usage: KeyUsageType.ENCRYPT_DECRYPT),
            KeyArn
        );
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

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_PublicKeyWithoutTheAlgorithm_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(Rs256, [1, 2, 3], signingAlgorithms: ["RSASSA_PSS_SHA_256"]),
            KeyArn
        );
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

        Assert.Contains(Rs256.SigningAlgorithm, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_DecryptionRow_WritesThePublicKey()
    {
        var spki = RandomNumberGenerator.GetBytes(294);
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(CreatePublicKey(RsaOaep256, spki), KeyArn);
        var provider = CreateProvider();
        var writer = new ArrayBufferWriter<byte>();

        await provider.GetPublicKeyAsync(
            CreatePublicKeyRequest(RsaOaep256),
            writer,
            CancellationToken.None
        );

        Assert.Equal(spki, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task GetPublicKeyAsync_DecryptionRowWithSigningUsage_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(RsaOaep256, [1, 2, 3], usage: KeyUsageType.SIGN_VERIFY),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(RsaOaep256),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains("is not a decryption key", exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_DecryptionRowWithoutTheEncryptionAlgorithm_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(RsaOaep256, [1, 2, 3], encryptionAlgorithms: ["RSAES_OAEP_SHA_1"]),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(RsaOaep256),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(RsaOaep256.EncryptionAlgorithm, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_KeyAgreementRowWithEncryptionUsage_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(EcdhEs, [1, 2, 3], usage: KeyUsageType.ENCRYPT_DECRYPT),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(EcdhEs),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains("is not a key agreement key", exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_KeyAgreementRowWithoutTheAgreementAlgorithm_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(
            CreatePublicKey(EcdhEs, [1, 2, 3], keyAgreementAlgorithms: ["OTHER"]),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .GetPublicKeyAsync(
                    CreatePublicKeyRequest(EcdhEs),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(EcdhEs.KeyAgreementAlgorithm, exception.Message);
    }

    [Fact]
    public async Task GetPublicKeyAsync_ResponseWithoutPublicKey_Throws()
    {
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupGetPublicKey(CreatePublicKey(Rs256, publicKey: null), KeyArn);
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

        Assert.Contains(KeyArn, exception.Message);
    }

    #endregion

    #region SignAsync Tests

    [Fact]
    public async Task SignAsync_DigestRow_SendsDigestToTheClientOfTheArnRegion()
    {
        var digest = RandomNumberGenerator.GetBytes(32);
        var signature = RandomNumberGenerator.GetBytes(384);
        SetupClient<SignResponse>("eu-central-1");
        SignRequest? sent = null;
        byte[]? sentMessage = null;
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SignRequest, CancellationToken>(
                (request, _) =>
                {
                    sent = request;
                    sentMessage = request.Message.ToArray();
                }
            )
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = OtherRegionKeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256,
                    Signature = new MemoryStream(signature),
                }
            )
            .Verifiable();
        var provider = CreateProvider(region: "us-west-2");
        var buffer = new byte[512];

        var written = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = OtherRegionKeyArn,
                Algorithm = Rs256,
                Data = digest,
            },
            buffer,
            CancellationToken.None
        );

        Assert.Equal(signature, buffer[..written]);
        var request = Assert.IsType<SignRequest>(sent);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(MessageType.DIGEST, request.MessageType);
        Assert.Equal(SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256, request.SigningAlgorithm);
        Assert.Equal(digest, sentMessage);
    }

    [Fact]
    public async Task SignAsync_RowWithoutDigest_SendsRawMessage()
    {
        var row = Rs256 with { DigestAlgorithm = null };
        var input = "signing input"u8.ToArray();
        var signature = RandomNumberGenerator.GetBytes(384);
        SetupClient<SignResponse>("us-west-2");
        SignRequest? sent = null;
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SignRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = KeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256,
                    Signature = new MemoryStream(signature),
                }
            )
            .Verifiable();
        var provider = CreateProvider();
        var buffer = new byte[512];

        var written = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = KeyArn,
                Algorithm = row,
                Data = input,
            },
            buffer,
            CancellationToken.None
        );

        Assert.Equal(signature, buffer[..written]);
        Assert.Equal(MessageType.RAW, Assert.IsType<SignRequest>(sent).MessageType);
    }

    [Fact]
    public async Task SignAsync_ArrayBackedData_SendsTheCallersMemoryWithoutCopying()
    {
        var data = RandomNumberGenerator.GetBytes(40);
        var original = data[4];
        var signature = RandomNumberGenerator.GetBytes(384);
        SetupClient<SignResponse>("us-west-2");
        byte[]? sentMessage = null;
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SignRequest, CancellationToken>(
                (request, _) =>
                {
                    data[4] ^= 0xFF;
                    sentMessage = request.Message.ToArray();
                }
            )
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = KeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256,
                    Signature = new MemoryStream(signature),
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = KeyArn,
                Algorithm = Rs256,
                Data = data.AsMemory(4, 32),
            },
            new byte[512],
            CancellationToken.None
        );

        var message = Assert.IsType<byte[]>(sentMessage);
        Assert.Equal(32, message.Length);
        Assert.Equal((byte)(original ^ 0xFF), message[0]);
    }

    [Fact]
    public async Task SignAsync_SignatureLongerThanTheBuffer_Throws()
    {
        SetupClient<SignResponse>("us-west-2");
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = KeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256,
                    Signature = new MemoryStream(RandomNumberGenerator.GetBytes(384)),
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyArn,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[383],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task SignAsync_ResponseNamesAnotherKey_ThrowsIOException()
    {
        SetupClient<SignResponse>("us-west-2");
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = OtherKeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256,
                    Signature = new MemoryStream([1, 2, 3]),
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyArn,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    new byte[512],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task SignAsync_ResponseNamesAnotherAlgorithm_ThrowsIOException()
    {
        SetupClient<SignResponse>("us-west-2");
        Client
            .Setup(x => x.SignAsync(It.IsAny<SignRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new SignResponse
                {
                    KeyId = KeyArn,
                    SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PSS_SHA_256,
                    Signature = new MemoryStream([1, 2, 3]),
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyArn,
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
    public async Task SignAsync_KeyIdIsNotAnArn_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = "not-an-arn",
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

    #endregion

    #region DecryptAsync Tests

    private void SetupDecrypt(DecryptResponse response, Action<DecryptRequest>? capture = null) =>
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DecryptRequest, CancellationToken>((request, _) => capture?.Invoke(request))
            .ReturnsAsync(response)
            .Verifiable();

    private static RemoteDecryptionRequest CreateDecryptionRequest(
        ReadOnlyMemory<byte> ciphertext,
        string keyId = KeyArn
    ) =>
        new()
        {
            KeyId = keyId,
            Algorithm = RsaOaep256,
            Ciphertext = ciphertext,
        };

    [Fact]
    public async Task DecryptAsync_SendsCiphertextAndAlgorithm_WritesPlaintext()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(256);
        var plaintext = RandomNumberGenerator.GetBytes(32);
        SetupClient<DecryptResponse>("eu-central-1");
        DecryptRequest? sent = null;
        byte[]? sentCiphertext = null;
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = OtherRegionKeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
                Plaintext = new MemoryStream(plaintext),
            },
            request =>
            {
                sent = request;
                sentCiphertext = request.CiphertextBlob.ToArray();
            }
        );
        var provider = CreateProvider(region: "us-west-2");
        var buffer = new byte[64];

        var written = await provider.DecryptAsync(
            CreateDecryptionRequest(ciphertext, OtherRegionKeyArn),
            buffer,
            CancellationToken.None
        );

        Assert.Equal(plaintext, buffer[..written]);
        var request = Assert.IsType<DecryptRequest>(sent);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256, request.EncryptionAlgorithm);
        Assert.Equal(ciphertext, sentCiphertext);
    }

    [Fact]
    public async Task DecryptAsync_ArrayBackedCiphertext_SendsTheCallersMemoryWithoutCopying()
    {
        var data = RandomNumberGenerator.GetBytes(40);
        var original = data[4];
        SetupClient<DecryptResponse>("us-west-2");
        byte[]? sentCiphertext = null;
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = KeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
                Plaintext = new MemoryStream([1, 2, 3]),
            },
            request =>
            {
                data[4] ^= 0xFF;
                sentCiphertext = request.CiphertextBlob.ToArray();
            }
        );
        var provider = CreateProvider();

        await provider.DecryptAsync(
            CreateDecryptionRequest(data.AsMemory(4, 32)),
            new byte[64],
            CancellationToken.None
        );

        var ciphertext = Assert.IsType<byte[]>(sentCiphertext);
        Assert.Equal(32, ciphertext.Length);
        Assert.Equal((byte)(original ^ 0xFF), ciphertext[0]);
    }

    [Fact]
    public async Task DecryptAsync_ResponsePlaintext_IsZeroedAfterTheCopy()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var responseBuffer = (byte[])plaintext.Clone();
        SetupClient<DecryptResponse>("us-west-2");
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = KeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
                Plaintext = new MemoryStream(
                    responseBuffer,
                    0,
                    responseBuffer.Length,
                    writable: true,
                    publiclyVisible: true
                ),
            }
        );
        var provider = CreateProvider();
        var buffer = new byte[64];

        var written = await provider.DecryptAsync(
            CreateDecryptionRequest(new byte[256]),
            buffer,
            CancellationToken.None
        );

        Assert.Equal(plaintext, buffer[..written]);
        Assert.All(responseBuffer, x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task DecryptAsync_InvalidCiphertext_ThrowsCryptographicException()
    {
        SetupClient<DecryptResponse>("us-west-2");
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCiphertextException("invalid"))
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
        Assert.IsType<InvalidCiphertextException>(exception.InnerException);
    }

    [Fact]
    public async Task DecryptAsync_ResponseNamesAnotherKey_ThrowsIOException()
    {
        SetupClient<DecryptResponse>("us-west-2");
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = OtherKeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
                Plaintext = new MemoryStream([1, 2, 3]),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DecryptAsync_ResponseNamesAnotherAlgorithm_ThrowsIOException()
    {
        SetupClient<DecryptResponse>("us-west-2");
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = KeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_1,
                Plaintext = new MemoryStream([1, 2, 3]),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(RsaOaep256.EncryptionAlgorithm, exception.Message);
    }

    [Fact]
    public async Task DecryptAsync_PlaintextLongerThanTheBuffer_Throws()
    {
        SetupClient<DecryptResponse>("us-west-2");
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = KeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
                Plaintext = new MemoryStream(RandomNumberGenerator.GetBytes(32)),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256]),
                    new byte[31],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DecryptAsync_ResponseWithoutPlaintext_Throws()
    {
        SetupClient<DecryptResponse>("us-west-2");
        SetupDecrypt(
            new DecryptResponse
            {
                KeyId = KeyArn,
                EncryptionAlgorithm = EncryptionAlgorithmSpec.RSAES_OAEP_SHA_256,
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DecryptAsync_KeyIdIsNotAnArn_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .DecryptAsync(
                    CreateDecryptionRequest(new byte[256], "not-an-arn"),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    #endregion

    #region DeriveSharedSecretAsync Tests

    private void SetupDeriveSharedSecret(
        DeriveSharedSecretResponse response,
        Action<DeriveSharedSecretRequest>? capture = null
    ) =>
        Client
            .Setup(x =>
                x.DeriveSharedSecretAsync(
                    It.IsAny<DeriveSharedSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DeriveSharedSecretRequest, CancellationToken>(
                (request, _) => capture?.Invoke(request)
            )
            .ReturnsAsync(response)
            .Verifiable();

    private static RemoteKeyAgreementRequest CreateAgreementRequest(
        ReadOnlyMemory<byte> peerSubjectPublicKeyInfo,
        string keyId = KeyArn
    ) =>
        new()
        {
            KeyId = keyId,
            Algorithm = EcdhEs,
            PeerSubjectPublicKeyInfo = peerSubjectPublicKeyInfo,
        };

    [Fact]
    public async Task DeriveSharedSecretAsync_SendsPeerKeyAndEcdh_WritesSecret()
    {
        var peerKey = RandomNumberGenerator.GetBytes(91);
        var secret = RandomNumberGenerator.GetBytes(32);
        SetupClient<DeriveSharedSecretResponse>("eu-central-1");
        DeriveSharedSecretRequest? sent = null;
        byte[]? sentPeerKey = null;
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = OtherRegionKeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.ECDH,
                SharedSecret = new MemoryStream(secret),
            },
            request =>
            {
                sent = request;
                sentPeerKey = request.PublicKey.ToArray();
            }
        );
        var provider = CreateProvider(region: "us-west-2");
        var buffer = new byte[64];

        var written = await provider.DeriveSharedSecretAsync(
            CreateAgreementRequest(peerKey, OtherRegionKeyArn),
            buffer,
            CancellationToken.None
        );

        Assert.Equal(secret, buffer[..written]);
        var request = Assert.IsType<DeriveSharedSecretRequest>(sent);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(KeyAgreementAlgorithmSpec.ECDH, request.KeyAgreementAlgorithm);
        Assert.Equal(peerKey, sentPeerKey);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_ResponseSharedSecret_IsZeroedAfterTheCopy()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var responseBuffer = (byte[])secret.Clone();
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = KeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.ECDH,
                SharedSecret = new MemoryStream(
                    responseBuffer,
                    0,
                    responseBuffer.Length,
                    writable: true,
                    publiclyVisible: true
                ),
            }
        );
        var provider = CreateProvider();
        var buffer = new byte[64];

        var written = await provider.DeriveSharedSecretAsync(
            CreateAgreementRequest(new byte[91]),
            buffer,
            CancellationToken.None
        );

        Assert.Equal(secret, buffer[..written]);
        Assert.All(responseBuffer, x => Assert.Equal(0, x));
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_InvalidKeyUsage_ThrowsCryptographicException()
    {
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        Client
            .Setup(x =>
                x.DeriveSharedSecretAsync(
                    It.IsAny<DeriveSharedSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidKeyUsageException("invalid"))
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
        Assert.IsType<InvalidKeyUsageException>(exception.InnerException);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_ResponseNamesAnotherKey_ThrowsIOException()
    {
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = OtherKeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.ECDH,
                SharedSecret = new MemoryStream([1, 2, 3]),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_ResponseNamesAnotherAlgorithm_ThrowsIOException()
    {
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = KeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.FindValue("OTHER"),
                SharedSecret = new MemoryStream([1, 2, 3]),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(EcdhEs.KeyAgreementAlgorithm, exception.Message);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_SecretLongerThanTheBuffer_Throws()
    {
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = KeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.ECDH,
                SharedSecret = new MemoryStream(RandomNumberGenerator.GetBytes(32)),
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91]),
                    new byte[31],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_ResponseWithoutSharedSecret_Throws()
    {
        SetupClient<DeriveSharedSecretResponse>("us-west-2");
        SetupDeriveSharedSecret(
            new DeriveSharedSecretResponse
            {
                KeyId = KeyArn,
                KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.ECDH,
            }
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91]),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task DeriveSharedSecretAsync_KeyIdIsNotAnArn_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .DeriveSharedSecretAsync(
                    CreateAgreementRequest(new byte[91], "not-an-arn"),
                    new byte[64],
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("request", exception.ParamName);
    }

    #endregion

    #region DestroyKeyAsync Tests

    [Fact]
    public async Task DestroyKeyAsync_SchedulesDeletionWithTheWindowInTheArnRegion()
    {
        SetupClient<ScheduleKeyDeletionResponse>("eu-central-1");
        ScheduleKeyDeletionRequest? sent = null;
        Client
            .Setup(x =>
                x.ScheduleKeyDeletionAsync(
                    It.IsAny<ScheduleKeyDeletionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ScheduleKeyDeletionRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new ScheduleKeyDeletionResponse())
            .Verifiable();
        var provider = CreateProvider(pendingWindowInDays: 14);

        await provider.DestroyKeyAsync(OtherRegionKeyArn, CancellationToken.None);

        var request = Assert.IsType<ScheduleKeyDeletionRequest>(sent);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(14, request.PendingWindowInDays);
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyNotFound_Completes()
    {
        SetupClient<ScheduleKeyDeletionResponse>("us-west-2");
        Client
            .Setup(x =>
                x.ScheduleKeyDeletionAsync(
                    It.IsAny<ScheduleKeyDeletionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new NotFoundException("missing"))
            .Verifiable();
        var provider = CreateProvider();

        await provider.DestroyKeyAsync(KeyArn, CancellationToken.None);
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyPendingDeletion_Completes()
    {
        SetupClient<ScheduleKeyDeletionResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        Client
            .Setup(x =>
                x.ScheduleKeyDeletionAsync(
                    It.IsAny<ScheduleKeyDeletionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new KMSInvalidStateException("pending deletion"))
            .Verifiable();
        Client
            .Setup(x =>
                x.DescribeKeyAsync(
                    It.Is<DescribeKeyRequest>(r => r.KeyId == KeyArn),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new DescribeKeyResponse
                {
                    KeyMetadata = new KeyMetadata { KeyState = KeyState.PendingDeletion },
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        await provider.DestroyKeyAsync(KeyArn, CancellationToken.None);
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyInAnotherState_Rethrows()
    {
        SetupClient<ScheduleKeyDeletionResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        var failure = new KMSInvalidStateException("invalid state");
        Client
            .Setup(x =>
                x.ScheduleKeyDeletionAsync(
                    It.IsAny<ScheduleKeyDeletionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(failure)
            .Verifiable();
        Client
            .Setup(x =>
                x.DescribeKeyAsync(
                    It.Is<DescribeKeyRequest>(r => r.KeyId == KeyArn),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new DescribeKeyResponse
                {
                    KeyMetadata = new KeyMetadata { KeyState = KeyState.Disabled },
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<KMSInvalidStateException>(() =>
            provider.DestroyKeyAsync(KeyArn, CancellationToken.None).AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task DestroyKeyAsync_OtherFailure_Propagates()
    {
        SetupClient<ScheduleKeyDeletionResponse>("us-west-2");
        Client
            .Setup(x =>
                x.ScheduleKeyDeletionAsync(
                    It.IsAny<ScheduleKeyDeletionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new KMSInternalException("outage"))
            .Verifiable();
        var provider = CreateProvider();

        await Assert.ThrowsAsync<KMSInternalException>(() =>
            provider.DestroyKeyAsync(KeyArn, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task DestroyKeyAsync_KeyIdIsNotAnArn_Throws()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.DestroyKeyAsync("not-an-arn", CancellationToken.None).AsTask()
        );

        Assert.Equal("keyId", exception.ParamName);
    }

    #endregion

    #region ListKeysAsync Tests

    private static DateTime CreationDate { get; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static async ValueTask<List<RemoteKeyListing>> ListAsync(
        AwsKmsRemoteKeyProvider provider,
        CancellationToken cancellationToken
    )
    {
        var listings = new List<RemoteKeyListing>();
        await foreach (
            var listing in provider.ListKeysAsync(
                new RemoteKeyListRequest { Deployment = "deployment-1" },
                cancellationToken
            )
        )
        {
            listings.Add(listing);
        }

        return listings;
    }

    private void SetupListKeys(string? marker, ListKeysResponse response) =>
        Client
            .Setup(x =>
                x.ListKeysAsync(
                    It.Is<ListKeysRequest>(r => r.Limit == 1000 && r.Marker == marker),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response)
            .Verifiable();

    private void SetupListTags(string arn, params (string Key, string Value)[] tags) =>
        Client
            .Setup(x =>
                x.ListResourceTagsAsync(
                    It.Is<ListResourceTagsRequest>(r => r.KeyId == arn && r.Limit == 50),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ListResourceTagsResponse
                {
                    Tags = tags.Select(x => new Tag { TagKey = x.Key, TagValue = x.Value })
                        .ToList(),
                }
            )
            .Verifiable();

    private void SetupDescribe(string arn, KeyManagerType manager, KeyState state) =>
        Client
            .Setup(x =>
                x.DescribeKeyAsync(
                    It.Is<DescribeKeyRequest>(r => r.KeyId == arn),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new DescribeKeyResponse
                {
                    KeyMetadata = new KeyMetadata
                    {
                        KeyManager = manager,
                        KeyState = state,
                        CreationDate = CreationDate,
                    },
                }
            )
            .Verifiable();

    private static ListKeysResponse CreateKeysPage(string? nextMarker, params string[] arns) =>
        new()
        {
            Keys = arns.Select(x => new KeyListEntry { KeyArn = x }).ToList(),
            Truncated = nextMarker is not null,
            NextMarker = nextMarker,
        };

    [Fact]
    public async Task ListKeysAsync_MatchingKey_ListsItWithItsTagsAndCreationDate()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(
            KeyArn,
            (AwsKmsRemoteKeyProvider.GroupTagKey, "ncid-group-1"),
            (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-1"),
            (AwsKmsRemoteKeyProvider.OwnerTagKey, "tenant-1"),
            (AwsKmsRemoteKeyProvider.SecretTagKey, "secret-1")
        );
        SetupDescribe(KeyArn, KeyManagerType.CUSTOMER, KeyState.Enabled);
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        var listing = Assert.Single(listings);
        Assert.Equal(KeyArn, listing.KeyId);
        Assert.Equal("ncid-group-1", listing.GroupName);
        Assert.Equal(new DateTimeOffset(CreationDate), listing.CreatedWhen);
        Assert.Equal("tenant-1", listing.OwnerId);
        Assert.Equal("secret-1", listing.SecretId);
    }

    [Fact]
    public async Task ListKeysAsync_KeyOfAnotherDeployment_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(
            KeyArn,
            (AwsKmsRemoteKeyProvider.GroupTagKey, "ncid-group-1"),
            (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-2")
        );
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_KeyWithoutTheGroupPrefix_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(
            KeyArn,
            (AwsKmsRemoteKeyProvider.GroupTagKey, "other-group-1"),
            (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-1")
        );
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_KeyWithoutTags_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(KeyArn);
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_KeyPendingDeletion_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(
            KeyArn,
            (AwsKmsRemoteKeyProvider.GroupTagKey, "ncid-group-1"),
            (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-1")
        );
        SetupDescribe(KeyArn, KeyManagerType.CUSTOMER, KeyState.PendingDeletion);
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_AwsManagedKey_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        SetupListTags(
            KeyArn,
            (AwsKmsRemoteKeyProvider.GroupTagKey, "ncid-group-1"),
            (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-1")
        );
        SetupDescribe(KeyArn, KeyManagerType.AWS, KeyState.Enabled);
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_TagsAccessDenied_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        Client
            .Setup(x =>
                x.ListResourceTagsAsync(
                    It.Is<ListResourceTagsRequest>(r => r.KeyId == KeyArn),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new AmazonKeyManagementServiceException("denied")
                {
                    ErrorCode = "AccessDeniedException",
                }
            )
            .Verifiable();
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_KeyDeletedBeforeItsTagsAreRead_SkipsIt()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupListKeys(null, CreateKeysPage(null, KeyArn));
        Client
            .Setup(x =>
                x.ListResourceTagsAsync(
                    It.Is<ListResourceTagsRequest>(r => r.KeyId == KeyArn),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new NotFoundException("missing"))
            .Verifiable();
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_TruncatedPage_FollowsNextMarker()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupClient<ListResourceTagsResponse>("us-west-2");
        SetupClient<DescribeKeyResponse>("us-west-2");
        SetupListKeys("marker-1", CreateKeysPage(null, OtherKeyArn));
        SetupListKeys(null, CreateKeysPage("marker-1", KeyArn));
        foreach (var arn in new[] { KeyArn, OtherKeyArn })
        {
            SetupListTags(
                arn,
                (AwsKmsRemoteKeyProvider.GroupTagKey, "ncid-group-1"),
                (AwsKmsRemoteKeyProvider.DeploymentTagKey, "deployment-1")
            );
            SetupDescribe(arn, KeyManagerType.CUSTOMER, KeyState.Enabled);
        }
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Equal([KeyArn, OtherKeyArn], listings.Select(x => x.KeyId));
    }

    [Fact]
    public async Task ListKeysAsync_ServiceReturnsNoKeys_ListsNothing()
    {
        SetupClient<ListKeysResponse>("us-west-2");
        SetupListKeys(null, new ListKeysResponse { Keys = null, Truncated = null });
        var provider = CreateProvider();

        var listings = await ListAsync(provider, CancellationToken.None);

        Assert.Empty(listings);
    }

    [Fact]
    public async Task ListKeysAsync_RegionUnset_Throws()
    {
        var provider = CreateProvider(region: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ListAsync(provider, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_IsAwsKms()
    {
        var provider = CreateProvider();

        Assert.Equal(RemoteKeyProviderNames.AwsKms, provider.Name);
    }

    #endregion
}
