// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

public class AwsKmsRemoteKeyProviderTests : IDisposable
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private const string OtherRegionKeyArn =
        "arn:aws:kms:eu-central-1:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private const string OtherKeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private static readonly RemoteSigningAlgorithm Rs256 =
        new AwsKmsSigningAlgorithms().Algorithms.Single(x =>
            x.Algorithm == "RS256" && x.KeySizeBits == 3072
        );

    private static readonly RemoteSigningAlgorithm Es256 =
        new AwsKmsSigningAlgorithms().Algorithms.Single(x => x.Algorithm == "ES256");

    private Mock<IAwsKmsClientFactory> ClientFactory { get; } = new(MockBehavior.Strict);

    private Mock<IAmazonKeyManagementService> Client { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        ClientFactory.Verify();
        Client.Verify();
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

    private static RemoteKeyCreationRequest CreateCreationRequest(RemoteSigningAlgorithm row) =>
        new()
        {
            OwnerId = "tenant-1",
            SecretId = "secret-1",
            Algorithm = row,
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
        RemoteSigningAlgorithm row,
        byte[] publicKey,
        KeyUsageType? usage = null,
        string? keySpec = null,
        List<string>? signingAlgorithms = null
    ) =>
        new()
        {
            PublicKey = new MemoryStream(publicKey),
            KeyUsage = usage ?? KeyUsageType.SIGN_VERIFY,
            KeySpec = KeySpec.FindValue(keySpec ?? row.KeySpec),
            SigningAlgorithms = signingAlgorithms ?? [row.SigningAlgorithm],
        };

    #region CreateKeyAsync Tests

    [Fact]
    public async Task CreateKeyAsync_SendsSpecUsageTagsAndDescription_ReturnsArnAndPublicKey()
    {
        var request = CreateCreationRequest(Rs256);
        var spki = RandomNumberGenerator.GetBytes(64);
        CreateKeyRequest? sent = null;
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        SetupGetPublicKey(CreatePublicKey(Rs256, spki), KeyArn);
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(KeyArn, result.KeyId);
        Assert.Equal(spki, result.SubjectPublicKeyInfo.ToArray());
        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.RSA_3072, createRequest.KeySpec);
        Assert.Equal(KeyUsageType.SIGN_VERIFY, createRequest.KeyUsage);
        Assert.Equal("NCode.Identity signing key secret-1 of tenant-1", createRequest.Description);
        Assert.Equal(4, createRequest.Tags.Count);
        Assert.Equal("tenant-1", Tag(createRequest, "ncode-identity:owner"));
        Assert.Equal("secret-1", Tag(createRequest, "ncode-identity:secret"));
        Assert.Equal("RS256", Tag(createRequest, "ncode-identity:algorithm"));
        Assert.Equal(request.GroupName, Tag(createRequest, "ncode-identity:group"));
    }

    [Fact]
    public async Task CreateKeyAsync_EllipticCurveRow_SendsCurveSpec()
    {
        var request = CreateCreationRequest(Es256);
        var spki = RandomNumberGenerator.GetBytes(91);
        CreateKeyRequest? sent = null;
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn, captured => sent = captured);
        SetupGetPublicKey(CreatePublicKey(Es256, spki), KeyArn);
        var provider = CreateProvider();

        var result = await provider.CreateKeyAsync(request, CancellationToken.None);

        Assert.Equal(spki, result.SubjectPublicKeyInfo.ToArray());
        var createRequest = Assert.IsType<CreateKeyRequest>(sent);
        Assert.Equal(KeySpec.ECC_NIST_P256, createRequest.KeySpec);
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
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupCreateKey("not-an-arn");
        var provider = CreateProvider();

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task CreateKeyAsync_PublicKeyOfAnotherSpec_Throws()
    {
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn);
        SetupGetPublicKey(CreatePublicKey(Rs256, [1, 2, 3], keySpec: "RSA_2048"), KeyArn);
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task CreateKeyAsync_PublicKeyOfAnotherUsage_Throws()
    {
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn);
        SetupGetPublicKey(
            CreatePublicKey(Rs256, [1, 2, 3], usage: KeyUsageType.ENCRYPT_DECRYPT),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task CreateKeyAsync_PublicKeyWithoutTheAlgorithm_Throws()
    {
        SetupClient<CreateKeyResponse>("us-west-2");
        SetupClient<GetPublicKeyResponse>("us-west-2");
        SetupCreateKey(KeyArn);
        SetupGetPublicKey(
            CreatePublicKey(Rs256, [1, 2, 3], signingAlgorithms: ["RSASSA_PSS_SHA_256"]),
            KeyArn
        );
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.CreateKeyAsync(CreateCreationRequest(Rs256), CancellationToken.None).AsTask()
        );

        Assert.Contains(Rs256.SigningAlgorithm, exception.Message);
    }

    private static string? Tag(CreateKeyRequest request, string key) =>
        request.Tags.Single(x => x.TagKey == key).TagValue;

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

        var result = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = OtherRegionKeyArn,
                Algorithm = Rs256,
                Data = digest,
            },
            CancellationToken.None
        );

        Assert.Equal(signature, result);
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

        var result = await provider.SignAsync(
            new RemoteSignatureRequest
            {
                KeyId = KeyArn,
                Algorithm = row,
                Data = input,
            },
            CancellationToken.None
        );

        Assert.Equal(signature, result);
        Assert.Equal(MessageType.RAW, Assert.IsType<SignRequest>(sent).MessageType);
    }

    [Fact]
    public async Task SignAsync_ResponseNamesAnotherKey_Throws()
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

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyArn,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Contains(KeyArn, exception.Message);
    }

    [Fact]
    public async Task SignAsync_ResponseNamesAnotherAlgorithm_Throws()
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

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .SignAsync(
                    new RemoteSignatureRequest
                    {
                        KeyId = KeyArn,
                        Algorithm = Rs256,
                        Data = new byte[32],
                    },
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

    #region Name Tests

    [Fact]
    public void Name_IsAwsKms()
    {
        var provider = CreateProvider();

        Assert.Equal(RemoteKeyProviderNames.AwsKms, provider.Name);
    }

    #endregion
}
