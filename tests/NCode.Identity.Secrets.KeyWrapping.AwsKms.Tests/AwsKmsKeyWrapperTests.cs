// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests;

public class AwsKmsKeyWrapperTests : IDisposable
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private const string AliasArn = "arn:aws:kms:us-west-2:111122223333:alias/ncode-identity-root";

    private const string AliasOtherRegion =
        "arn:aws:kms:eu-central-1:111122223333:alias/tenant-root";

    private const string OtherRegionKeyArn =
        "arn:aws:kms:eu-central-1:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private const string ExpectedContextKey = "ncode-identity:root-context";

    private const string RoleArn = "arn:aws:iam::444455556666:role/ncode-identity-tenant-root";

    private static readonly KeyAccess Access = new(RoleArn, "tenant:acme");

    private Mock<IAwsKmsClientFactory> ClientFactory { get; } = new(MockBehavior.Strict);

    private Mock<IAmazonKeyManagementService> Client { get; } = new(MockBehavior.Strict);

    private CapturedRequest? EncryptCall { get; set; }

    private CapturedRequest? DecryptCall { get; set; }

    public void Dispose()
    {
        ClientFactory.Verify();
        Client.Verify();
    }

    // The provider disposes the streams it sends once the call returns, so the test keeps a copy of what was sent.
    private sealed record CapturedRequest(
        string? KeyId,
        byte[] Data,
        EncryptionAlgorithmSpec? Algorithm,
        Dictionary<string, string> EncryptionContext
    );

    private AwsKmsKeyWrapper CreateProvider(string? keyArn) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new AwsKmsKeyWrapperOptions { KeyArn = keyArn }
            )
        );

    // The provider zeroes an exposed buffer once it has copied it, so the stream is created over a copy.
    private static MemoryStream CreateStream(byte[] bytes, bool bufferIsVisible)
    {
        var copy = bytes.ToArray();
        return new MemoryStream(
            copy,
            0,
            copy.Length,
            writable: false,
            publiclyVisible: bufferIsVisible
        );
    }

    private static WrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            WrapperName = KeyWrapperNames.AwsKms,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private void SetupClient(string region, KeyAccess? access = null) =>
        ClientFactory.Setup(x => x.GetClient(region, access)).Returns(Client.Object).Verifiable();

    private void SetupEncrypt(EncryptResponse response, CancellationToken cancellationToken) =>
        Client
            .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), cancellationToken))
            .Callback<EncryptRequest, CancellationToken>(
                (request, _) =>
                    EncryptCall = new CapturedRequest(
                        request.KeyId,
                        request.Plaintext.ToArray(),
                        request.EncryptionAlgorithm,
                        new Dictionary<string, string>(request.EncryptionContext)
                    )
            )
            .ReturnsAsync(response)
            .Verifiable();

    private void SetupDecrypt(DecryptResponse response, CancellationToken cancellationToken) =>
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), cancellationToken))
            .Callback<DecryptRequest, CancellationToken>(
                (request, _) =>
                    DecryptCall = new CapturedRequest(
                        request.KeyId,
                        request.CiphertextBlob.ToArray(),
                        request.EncryptionAlgorithm,
                        new Dictionary<string, string>(request.EncryptionContext)
                    )
            )
            .ReturnsAsync(response)
            .Verifiable();

    private void SetupDecryptThrows(Exception exception) =>
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .Verifiable();

    private void SetupEncryptThrows(Exception exception) =>
        Client
            .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .Verifiable();

    private void SetupRelease(string region, KeyAccess access) =>
        ClientFactory.Setup(x => x.Release(region, access)).Verifiable();

    private void SetupConfirm(string region, KeyAccess access) =>
        ClientFactory.Setup(x => x.Confirm(region, access)).Verifiable();

    #region WrapAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrapAsync_SendsKeyArnPlaintextAndEncryptionContext(
        bool ciphertextBufferIsVisible
    )
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        using var cancellationSource = new CancellationTokenSource();
        SetupClient("us-west-2");
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = KeyArn,
                CiphertextBlob = CreateStream(ciphertext, ciphertextBufferIsVisible),
            },
            cancellationSource.Token
        );
        var provider = CreateProvider(KeyArn);

        var wrappedKey = await provider.WrapAsync(key, context, cancellationSource.Token);

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(KeyArn, request.KeyId);
        Assert.Equal(key, request.Data);
        Assert.Equal(EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT, request.Algorithm);
        var contextEntry = Assert.Single(request.EncryptionContext);
        Assert.Equal(ExpectedContextKey, contextEntry.Key);
        Assert.Equal(Base64Url.EncodeToString(context), contextEntry.Value);
        Assert.Equal(KeyWrapperNames.AwsKms, wrappedKey.WrapperName);
        Assert.Equal(KeyArn, wrappedKey.KeyId);
        Assert.Equal(Base64Url.EncodeToString(ciphertext), wrappedKey.Ciphertext);
    }

    [Fact]
    public async Task WrapAsync_AliasConfigured_ReturnsResponseKeyArn()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupClient("us-west-2");
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = KeyArn,
                CiphertextBlob = CreateStream(ciphertext, bufferIsVisible: true),
            },
            CancellationToken.None
        );
        var provider = CreateProvider(AliasArn);

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(AliasArn, request.KeyId);
        Assert.Equal(KeyArn, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_KeyArnUnset_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(keyArn: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region Referenced Key Tests

    [Fact]
    public async Task WrapAsync_ReferencedKey_SendsReferenceToTheClientOfItsRegion()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupClient("eu-central-1");
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = OtherRegionKeyArn,
                CiphertextBlob = CreateStream(ciphertext, bufferIsVisible: true),
            },
            CancellationToken.None
        );
        var provider = CreateProvider(KeyArn);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(AliasOtherRegion, null),
            key,
            context,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(AliasOtherRegion, request.KeyId);
        Assert.Equal(key, request.Data);
        var contextEntry = Assert.Single(request.EncryptionContext);
        Assert.Equal(ExpectedContextKey, contextEntry.Key);
        Assert.Equal(Base64Url.EncodeToString(context), contextEntry.Value);
        Assert.Equal(KeyWrapperNames.AwsKms, wrappedKey.WrapperName);
        Assert.Equal(OtherRegionKeyArn, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyInvalid_ThrowsWithoutCallingTheClient()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("not-an-arn", null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task UnwrapAsync_KeyWrappedUnderReference_UnwrapsThroughItsKeyId()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupClient("eu-central-1");
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = OtherRegionKeyArn,
                CiphertextBlob = CreateStream(ciphertext, bufferIsVisible: true),
            },
            CancellationToken.None
        );
        SetupClient("eu-central-1");
        SetupDecrypt(
            new DecryptResponse { Plaintext = CreateStream(key, bufferIsVisible: false) },
            CancellationToken.None
        );
        var provider = CreateProvider(keyArn: null);
        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(OtherRegionKeyArn, null),
            key,
            context,
            CancellationToken.None
        );
        var destination = new ArrayBufferWriter<byte>();

        await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None);

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(key, destination.WrittenSpan.ToArray());
    }

    [Theory]
    [InlineData(KeyArn, true)]
    [InlineData(AliasArn, true)]
    [InlineData("not-an-arn", false)]
    [InlineData("", false)]
    [InlineData("arn:aws:s3:::bucket", false)]
    public void TryValidateKeyReference_Reference_ReportsSyntax(string reference, bool expected)
    {
        var provider = CreateProvider(keyArn: null);

        var valid = provider.TryValidateKeyReference(reference, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.StartsWith("must be the ARN of a KMS key or alias", failure);
    }

    #endregion

    #region Access Tests

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccess_ReceivesAccessFromTheClientFactory()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupClient("eu-central-1", Access);
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = OtherRegionKeyArn,
                CiphertextBlob = CreateStream(ciphertext, bufferIsVisible: true),
            },
            CancellationToken.None
        );
        SetupConfirm("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(AliasOtherRegion, Access),
            key,
            context,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(AliasOtherRegion, request.KeyId);
        Assert.Equal(OtherRegionKeyArn, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccess_ReceivesNoAccessFromTheClientFactory()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupClient("eu-central-1", access: null);
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = OtherRegionKeyArn,
                CiphertextBlob = CreateStream(ciphertext, bufferIsVisible: true),
            },
            CancellationToken.None
        );
        var provider = CreateProvider(keyArn: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(AliasOtherRegion, Access: null),
            key,
            context,
            CancellationToken.None
        );

        Assert.Equal(OtherRegionKeyArn, wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidReference_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("not-an-arn", Access),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidRoleArn_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(AliasOtherRegion, new KeyAccess("not-a-role", "tenant:acme")),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccess_ReceivesAccessFromTheClientFactory()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        SetupClient("eu-central-1", Access);
        SetupDecrypt(
            new DecryptResponse { Plaintext = CreateStream(plaintext, bufferIsVisible: false) },
            CancellationToken.None
        );
        SetupConfirm("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        await provider.UnwrapAsync(
            wrappedKey,
            Access,
            context,
            destination,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_WithNullAccess_Throws()
    {
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64));
        var provider = CreateProvider(keyArn: null);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    null!,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Theory]
    [InlineData(RoleArn, true)]
    [InlineData("arn:aws-us-gov:iam::444455556666:role/path/tenant-root", true)]
    [InlineData("not-a-role", false)]
    [InlineData("", false)]
    [InlineData(KeyArn, false)]
    public void TryValidateAccessIdentity_Identity_ReportsSyntax(string identity, bool expected)
    {
        var provider = CreateProvider(keyArn: null);

        var valid = provider.TryValidateAccessIdentity(identity, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.StartsWith("must be the ARN of an IAM role", failure);
    }

    #endregion

    #region Release Tests

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessSucceeds_ConfirmsTheClientAndReleasesNothing()
    {
        SetupClient("eu-central-1", Access);
        SetupEncrypt(
            new EncryptResponse
            {
                KeyId = OtherRegionKeyArn,
                CiphertextBlob = CreateStream(
                    RandomNumberGenerator.GetBytes(64),
                    bufferIsVisible: true
                ),
            },
            CancellationToken.None
        );
        SetupConfirm("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        await provider.WrapAsync(
            new ReferencedKey(AliasOtherRegion, Access),
            RandomNumberGenerator.GetBytes(32),
            "root-context"u8.ToArray(),
            CancellationToken.None
        );

        ClientFactory.Verify(x => x.Confirm("eu-central-1", Access), Times.Once);
        ClientFactory.Verify(
            x => x.Release(It.IsAny<string>(), It.IsAny<KeyAccess>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessSucceeds_ConfirmsTheClientAndReleasesNothing()
    {
        SetupClient("eu-central-1", Access);
        SetupDecrypt(
            new DecryptResponse
            {
                Plaintext = CreateStream(
                    RandomNumberGenerator.GetBytes(32),
                    bufferIsVisible: false
                ),
            },
            CancellationToken.None
        );
        SetupConfirm("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        await provider.UnwrapAsync(
            CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64)),
            Access,
            "root-context"u8.ToArray(),
            new ArrayBufferWriter<byte>(),
            CancellationToken.None
        );

        ClientFactory.Verify(x => x.Confirm("eu-central-1", Access), Times.Once);
        ClientFactory.Verify(
            x => x.Release(It.IsAny<string>(), It.IsAny<KeyAccess>()),
            Times.Never
        );
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessFails_ReleasesTheClient()
    {
        var failure = new InvalidOperationException("The role cannot be assumed.");
        SetupClient("eu-central-1", Access);
        SetupEncryptThrows(failure);
        SetupRelease("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(AliasOtherRegion, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccessFails_ReleasesNothing()
    {
        var failure = new InvalidOperationException("The service is unavailable.");
        SetupClient("eu-central-1", access: null);
        SetupEncryptThrows(failure);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(AliasOtherRegion, Access: null),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessIsCanceled_ReleasesNothing()
    {
        var failure = new OperationCanceledException();
        SetupClient("eu-central-1", Access);
        SetupEncryptThrows(failure);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(AliasOtherRegion, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessFails_ReleasesTheClient()
    {
        var failure = new InvalidOperationException("The role cannot be assumed.");
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64));
        SetupClient("eu-central-1", Access);
        SetupDecryptThrows(failure);
        SetupRelease("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessRejectedByTheService_ReleasesTheClientAndThrowsCryptographicException()
    {
        var failure = new InvalidCiphertextException("The ciphertext is invalid.");
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64));
        SetupClient("eu-central-1", Access);
        SetupDecryptThrows(failure);
        SetupRelease("eu-central-1", Access);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_WithoutAccessFails_ReleasesNothing()
    {
        var failure = new InvalidOperationException("The service is unavailable.");
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64));
        SetupClient("eu-central-1");
        SetupDecryptThrows(failure);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task UnwrapAsync_WithAccessIsCanceled_ReleasesNothing()
    {
        var failure = new OperationCanceledException();
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, RandomNumberGenerator.GetBytes(64));
        SetupClient("eu-central-1", Access);
        SetupDecryptThrows(failure);
        var provider = CreateProvider(keyArn: null);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider
                .UnwrapAsync(
                    wrappedKey,
                    Access,
                    "root-context"u8.ToArray(),
                    new ArrayBufferWriter<byte>(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    #endregion

    #region UnwrapAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnwrapAsync_SendsRecordedKeyArnAndEncryptionContext_WritesPlaintextToDestination(
        bool plaintextBufferIsVisible
    )
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(OtherRegionKeyArn, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        using var cancellationSource = new CancellationTokenSource();
        SetupClient("eu-central-1");
        SetupDecrypt(
            new DecryptResponse { Plaintext = CreateStream(plaintext, plaintextBufferIsVisible) },
            cancellationSource.Token
        );
        var provider = CreateProvider(KeyArn);

        await provider.UnwrapAsync(wrappedKey, context, destination, cancellationSource.Token);

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(ciphertext, request.Data);
        Assert.Equal(EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT, request.Algorithm);
        var contextEntry = Assert.Single(request.EncryptionContext);
        Assert.Equal(ExpectedContextKey, contextEntry.Key);
        Assert.Equal(Base64Url.EncodeToString(context), contextEntry.Value);
        Assert.Equal(plaintext.Length, destination.WrittenCount);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_InvalidCiphertext_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyArn, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var kmsException = new InvalidCiphertextException("The ciphertext is invalid.");
        SetupClient("us-west-2");
        SetupDecryptThrows(kmsException);
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(kmsException, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_IncorrectKey_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyArn, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var kmsException = new IncorrectKeyException("The key does not match the ciphertext.");
        SetupClient("us-west-2");
        SetupDecryptThrows(kmsException);
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(kmsException, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_KmsInternalError_Propagates()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyArn, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var kmsException = new KMSInternalException("The service failed.");
        SetupClient("us-west-2");
        SetupDecryptThrows(kmsException);
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<KMSInternalException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(kmsException, exception);
    }

    [Fact]
    public async Task UnwrapAsync_MalformedKeyArn_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey("not-an-arn", ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyArn);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("AAAAA")]
    [InlineData("AA!A")]
    public async Task UnwrapAsync_MalformedCiphertext_ThrowsCryptographicException(
        string ciphertext
    )
    {
        var context = "root-context"u8.ToArray();
        var wrappedKey = new WrappedKey
        {
            WrapperName = KeyWrapperNames.AwsKms,
            KeyId = KeyArn,
            Ciphertext = ciphertext,
        };
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyArn);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
