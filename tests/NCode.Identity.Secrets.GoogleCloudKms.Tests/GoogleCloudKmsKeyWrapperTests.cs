// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class GoogleCloudKmsKeyWrapperTests : BaseTests
{
    private const string CryptoKeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private const string TenantCryptoKeyName =
        "projects/tenant-project/locations/europe-west1/keyRings/tenants/cryptoKeys/tenant-root";

    private const string CryptoKeyVersionName = CryptoKeyName + "/cryptoKeyVersions/3";

    private const string OtherCryptoKeyVersionName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/other/cryptoKeyVersions/3";

    private const string ServiceAccountEmail =
        "ncode-identity@tenant-project.iam.gserviceaccount.com";

    private static KeyAccess Access { get; } = new(ServiceAccountEmail, "tenant:acme");

    private Mock<IGoogleCloudKmsClientFactory> ClientFactory { get; }

    private Mock<KeyManagementServiceClient> Client { get; }

    private CapturedRequest? EncryptCall { get; set; }

    private CapturedRequest? DecryptCall { get; set; }

    public GoogleCloudKmsKeyWrapperTests()
    {
        ClientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        Client = CreateStrictMock<KeyManagementServiceClient>();
    }

    // The provider wraps its buffers in the request without a copy and may reuse them once the call returns, so the
    // test keeps a copy of what was sent.
    private sealed record CapturedRequest(
        string Name,
        byte[] Data,
        byte[] AdditionalAuthenticatedData,
        long? DataCrc32C,
        long? AdditionalAuthenticatedDataCrc32C
    );

    private GoogleCloudKmsKeyWrapper CreateProvider(string? keyName) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new GoogleCloudKmsKeyWrapperOptions { KeyName = keyName }
            )
        );

    private static EncryptResponse CreateEncryptResponse(string name, byte[] ciphertext) =>
        new()
        {
            Name = name,
            Ciphertext = ByteString.CopyFrom(ciphertext),
            CiphertextCrc32C = GoogleCloudKmsChecksum.Compute(ciphertext),
            VerifiedPlaintextCrc32C = true,
            VerifiedAdditionalAuthenticatedDataCrc32C = true,
        };

    private static DecryptResponse CreateDecryptResponse(byte[] plaintext) =>
        new()
        {
            Plaintext = ByteString.CopyFrom(plaintext),
            PlaintextCrc32C = GoogleCloudKmsChecksum.Compute(plaintext),
        };

    private static WrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            WrapperName = KeyWrapperNames.GoogleCloudKms,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private static RpcException CreateRpcException(StatusCode statusCode) =>
        new(new Status(statusCode, "x"));

    // The factory runs the operation of the provider with the client mock, as it runs it with a client of the access.
    private void SetupEncryptClient(
        CancellationToken cancellationToken,
        KeyAccess? access = null
    ) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    access,
                    It.IsAny<
                        Func<
                            KeyManagementServiceClient,
                            CancellationToken,
                            ValueTask<EncryptResponse>
                        >
                    >(),
                    cancellationToken
                )
            )
            .Returns(
                (
                    KeyAccess? _,
                    Func<
                        KeyManagementServiceClient,
                        CancellationToken,
                        ValueTask<EncryptResponse>
                    > operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupDecryptClient(
        CancellationToken cancellationToken,
        KeyAccess? access = null
    ) =>
        ClientFactory
            .Setup(x =>
                x.ExecuteAsync(
                    access,
                    It.IsAny<
                        Func<
                            KeyManagementServiceClient,
                            CancellationToken,
                            ValueTask<DecryptResponse>
                        >
                    >(),
                    cancellationToken
                )
            )
            .Returns(
                (
                    KeyAccess? _,
                    Func<
                        KeyManagementServiceClient,
                        CancellationToken,
                        ValueTask<DecryptResponse>
                    > operation,
                    CancellationToken token
                ) => operation(Client.Object, token)
            )
            .Verifiable();

    private void SetupEncrypt(EncryptResponse response, CancellationToken cancellationToken) =>
        Client
            .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), cancellationToken))
            .Callback<EncryptRequest, CancellationToken>(
                (request, _) =>
                    EncryptCall = new CapturedRequest(
                        request.Name,
                        request.Plaintext.ToByteArray(),
                        request.AdditionalAuthenticatedData.ToByteArray(),
                        request.PlaintextCrc32C,
                        request.AdditionalAuthenticatedDataCrc32C
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
                        request.Name,
                        request.Ciphertext.ToByteArray(),
                        request.AdditionalAuthenticatedData.ToByteArray(),
                        request.CiphertextCrc32C,
                        request.AdditionalAuthenticatedDataCrc32C
                    )
            )
            .ReturnsAsync(response)
            .Verifiable();

    private void SetupEncryptThrows(Exception exception) =>
        Client
            .Setup(x => x.EncryptAsync(It.IsAny<EncryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .Verifiable();

    private void SetupDecryptThrows(Exception exception, Times? times = null) =>
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .Verifiable(times ?? Times.AtLeastOnce());

    #region WrapAsync Tests

    [Fact]
    public async Task WrapAsync_ServiceConfirmsIntegrity_SendsChecksummedRequestAndReturnsWrappedKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        using var cancellationSource = new CancellationTokenSource();
        SetupEncryptClient(cancellationSource.Token);
        SetupEncrypt(
            CreateEncryptResponse(CryptoKeyVersionName, ciphertext),
            cancellationSource.Token
        );
        var provider = CreateProvider(CryptoKeyName);

        var wrappedKey = await provider.WrapAsync(key, context, cancellationSource.Token);

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(CryptoKeyName, request.Name);
        Assert.Equal(key, request.Data);
        Assert.Equal(context, request.AdditionalAuthenticatedData);
        Assert.Equal(GoogleCloudKmsChecksum.Compute(key), request.DataCrc32C);
        Assert.Equal(
            GoogleCloudKmsChecksum.Compute(context),
            request.AdditionalAuthenticatedDataCrc32C
        );
        Assert.Equal(KeyWrapperNames.GoogleCloudKms, wrappedKey.WrapperName);
        Assert.Equal(CryptoKeyVersionName, wrappedKey.KeyId);
        Assert.Equal(Base64Url.EncodeToString(ciphertext), wrappedKey.Ciphertext);
    }

    [Fact]
    public async Task WrapAsync_KeyNameUnset_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(keyName: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task WrapAsync_IntegrityNotConfirmed_ThrowsIOException(
        bool plaintextVerified,
        bool additionalAuthenticatedDataVerified,
        bool ciphertextChecksumMatches
    )
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var response = CreateEncryptResponse(CryptoKeyVersionName, ciphertext);
        response.VerifiedPlaintextCrc32C = plaintextVerified;
        response.VerifiedAdditionalAuthenticatedDataCrc32C = additionalAuthenticatedDataVerified;
        if (!ciphertextChecksumMatches)
        {
            response.CiphertextCrc32C ^= 1;
        }

        SetupEncryptClient(CancellationToken.None);
        SetupEncrypt(response, CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Theory]
    [InlineData(CryptoKeyName)]
    [InlineData(OtherCryptoKeyVersionName)]
    public async Task WrapAsync_ResponseNameNotVersionOfCryptoKey_ThrowsIOException(
        string responseName
    )
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None);
        SetupEncrypt(CreateEncryptResponse(responseName, ciphertext), CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ReferencedKey_SendsReferenceAndReturnsItsVersion()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None);
        SetupEncrypt(
            CreateEncryptResponse(TenantCryptoKeyName + "/cryptoKeyVersions/7", ciphertext),
            CancellationToken.None
        );
        var provider = CreateProvider(CryptoKeyName);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantCryptoKeyName, null),
            key,
            context,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(TenantCryptoKeyName, request.Name);
        Assert.Equal(key, request.Data);
        Assert.Equal(context, request.AdditionalAuthenticatedData);
        Assert.Equal(KeyWrapperNames.GoogleCloudKms, wrappedKey.WrapperName);
        Assert.Equal(TenantCryptoKeyName + "/cryptoKeyVersions/7", wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyReportsVersionOfAnotherCryptoKey_ThrowsIOException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None);
        SetupEncrypt(
            CreateEncryptResponse(CryptoKeyVersionName, ciphertext),
            CancellationToken.None
        );
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantCryptoKeyName, null),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Theory]
    [InlineData(CryptoKeyVersionName)]
    [InlineData("not-a-key")]
    public async Task WrapAsync_ReferencedKeyInvalid_ThrowsWithoutCallingTheClient(string reference)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(new ReferencedKey(reference, null), key, context, CancellationToken.None)
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccess_RunsTheCallThroughTheAccess()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None, Access);
        SetupEncrypt(
            CreateEncryptResponse(TenantCryptoKeyName + "/cryptoKeyVersions/7", ciphertext),
            CancellationToken.None
        );
        var provider = CreateProvider(keyName: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantCryptoKeyName, Access),
            key,
            context,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(EncryptCall);
        Assert.Equal(TenantCryptoKeyName, request.Name);
        Assert.Equal(TenantCryptoKeyName + "/cryptoKeyVersions/7", wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccess_RunsTheCallWithoutAccess()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None, access: null);
        SetupEncrypt(
            CreateEncryptResponse(TenantCryptoKeyName + "/cryptoKeyVersions/7", ciphertext),
            CancellationToken.None
        );
        var provider = CreateProvider(keyName: null);

        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantCryptoKeyName, Access: null),
            key,
            context,
            CancellationToken.None
        );

        Assert.Equal(TenantCryptoKeyName + "/cryptoKeyVersions/7", wrappedKey.KeyId);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidReference_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey("not-a-key", Access),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithInvalidServiceAccountEmail_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(
                        TenantCryptoKeyName,
                        new KeyAccess("not-a-service-account", "tenant:acme")
                    ),
                    key,
                    context,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal("referencedKey", exception.ParamName);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The service account cannot be impersonated.");
        SetupEncryptClient(CancellationToken.None, Access);
        SetupEncryptThrows(failure);
        var provider = CreateProvider(keyName: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantCryptoKeyName, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithoutAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The service is unavailable.");
        SetupEncryptClient(CancellationToken.None, access: null);
        SetupEncryptThrows(failure);
        var provider = CreateProvider(keyName: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantCryptoKeyName, Access: null),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task WrapAsync_ReferencedKeyWithAccessIsCanceled_Propagates()
    {
        var failure = new OperationCanceledException();
        SetupEncryptClient(CancellationToken.None, Access);
        SetupEncryptThrows(failure);
        var provider = CreateProvider(keyName: null);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider
                .WrapAsync(
                    new ReferencedKey(TenantCryptoKeyName, Access),
                    RandomNumberGenerator.GetBytes(32),
                    "root-context"u8.ToArray(),
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Same(failure, exception);
    }

    #endregion

    #region UnwrapAsync Tests

    [Fact]
    public async Task UnwrapAsync_WithAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The service account cannot be impersonated.");
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, RandomNumberGenerator.GetBytes(64));
        SetupDecryptClient(CancellationToken.None, Access);
        SetupDecryptThrows(failure);
        var provider = CreateProvider(keyName: null);

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
    public async Task UnwrapAsync_WithAccessRejectedByTheService_ThrowsCryptographicException()
    {
        var failure = CreateRpcException(StatusCode.InvalidArgument);
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, RandomNumberGenerator.GetBytes(64));
        SetupDecryptClient(CancellationToken.None, Access);
        SetupDecryptThrows(failure, Times.Exactly(2));
        var provider = CreateProvider(keyName: null);

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
    public async Task UnwrapAsync_WithoutAccessFails_Propagates()
    {
        var failure = new InvalidOperationException("The service is unavailable.");
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, RandomNumberGenerator.GetBytes(64));
        SetupDecryptClient(CancellationToken.None, access: null);
        SetupDecryptThrows(failure);
        var provider = CreateProvider(keyName: null);

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
    public async Task UnwrapAsync_WithAccessIsCanceled_Propagates()
    {
        var failure = new OperationCanceledException();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, RandomNumberGenerator.GetBytes(64));
        SetupDecryptClient(CancellationToken.None, Access);
        SetupDecryptThrows(failure);
        var provider = CreateProvider(keyName: null);

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

    [Fact]
    public async Task UnwrapAsync_SendsCryptoKeyNameAndChecksums_WritesPlaintextToDestination()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var response = CreateDecryptResponse(plaintext);
        using var cancellationSource = new CancellationTokenSource();
        SetupDecryptClient(cancellationSource.Token);
        SetupDecrypt(response, cancellationSource.Token);
        var provider = CreateProvider(CryptoKeyName);

        await provider.UnwrapAsync(wrappedKey, context, destination, cancellationSource.Token);

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(CryptoKeyName, request.Name);
        Assert.Equal(ciphertext, request.Data);
        Assert.Equal(context, request.AdditionalAuthenticatedData);
        Assert.Equal(GoogleCloudKmsChecksum.Compute(ciphertext), request.DataCrc32C);
        Assert.Equal(
            GoogleCloudKmsChecksum.Compute(context),
            request.AdditionalAuthenticatedDataCrc32C
        );
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
        Assert.Equal(new byte[plaintext.Length], response.Plaintext.ToByteArray());
    }

    [Fact]
    public async Task UnwrapAsync_PlaintextChecksumMismatch_ThrowsIOException()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var response = CreateDecryptResponse(plaintext);
        response.PlaintextCrc32C ^= 1;
        SetupDecryptClient(CancellationToken.None);
        SetupDecrypt(response, CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Equal(0, destination.WrittenCount);
        Assert.Equal(new byte[plaintext.Length], response.Plaintext.ToByteArray());
    }

    [Fact]
    public async Task UnwrapAsync_InvalidArgumentThenSuccess_RetriesAndWritesPlaintext()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var calls = 0;
        SetupDecryptClient(CancellationToken.None);
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), CancellationToken.None))
            .Returns(() =>
                ++calls == 1
                    ? Task.FromException<DecryptResponse>(
                        CreateRpcException(StatusCode.InvalidArgument)
                    )
                    : Task.FromResult(CreateDecryptResponse(plaintext))
            )
            .Verifiable(Times.Exactly(2));
        var provider = CreateProvider(CryptoKeyName);

        await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None);

        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_InvalidArgumentTwice_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var rpcException = CreateRpcException(StatusCode.InvalidArgument);
        SetupDecryptClient(CancellationToken.None);
        SetupDecryptThrows(rpcException, Times.Exactly(2));
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(rpcException, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_ServiceUnavailable_PropagatesAfterOneCall()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var rpcException = CreateRpcException(StatusCode.Unavailable);
        SetupDecryptClient(CancellationToken.None);
        SetupDecryptThrows(rpcException, Times.Once());
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(rpcException, exception);
    }

    [Fact]
    public async Task UnwrapAsync_KeyIdWithoutVersion_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("!!!!")]
    public async Task UnwrapAsync_MalformedCiphertext_ThrowsCryptographicException(
        string ciphertext
    )
    {
        var context = "root-context"u8.ToArray();
        var wrappedKey = new WrappedKey
        {
            WrapperName = KeyWrapperNames.GoogleCloudKms,
            KeyId = CryptoKeyVersionName,
            Ciphertext = ciphertext,
        };
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task UnwrapAsync_KeyWrappedUnderReference_UnwrapsThroughItsVersion()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        SetupEncryptClient(CancellationToken.None);
        SetupEncrypt(
            CreateEncryptResponse(TenantCryptoKeyName + "/cryptoKeyVersions/7", ciphertext),
            CancellationToken.None
        );
        SetupDecryptClient(CancellationToken.None);
        SetupDecrypt(CreateDecryptResponse(key), CancellationToken.None);
        var provider = CreateProvider(keyName: null);
        var wrappedKey = await provider.WrapAsync(
            new ReferencedKey(TenantCryptoKeyName, null),
            key,
            context,
            CancellationToken.None
        );
        var destination = new ArrayBufferWriter<byte>();

        await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None);

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(TenantCryptoKeyName, request.Name);
        Assert.Equal(key, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_WithAccess_RunsTheCallThroughTheAccess()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        SetupDecryptClient(CancellationToken.None, Access);
        SetupDecrypt(CreateDecryptResponse(plaintext), CancellationToken.None);
        var provider = CreateProvider(keyName: null);

        await provider.UnwrapAsync(
            wrappedKey,
            Access,
            context,
            destination,
            CancellationToken.None
        );

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(CryptoKeyName, request.Name);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public async Task UnwrapAsync_WithNullAccess_Throws()
    {
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, RandomNumberGenerator.GetBytes(64));
        var provider = CreateProvider(keyName: null);

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

    #endregion

    #region TryValidateKeyReference Tests

    [Theory]
    [InlineData(CryptoKeyName, true)]
    [InlineData(CryptoKeyVersionName, false)]
    [InlineData("not-a-key", false)]
    [InlineData("", false)]
    public void TryValidateKeyReference_Reference_ReportsSyntax(string reference, bool expected)
    {
        var provider = CreateProvider(keyName: null);

        var valid = provider.TryValidateKeyReference(reference, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.StartsWith("must be the resource name of a Cloud KMS crypto key", failure);
    }

    #endregion

    #region TryValidateAccessIdentity Tests

    [Theory]
    [InlineData(ServiceAccountEmail, true)]
    [InlineData("tenant-root@my-project.iam.gserviceaccount.com", true)]
    [InlineData("not-a-service-account", false)]
    [InlineData("", false)]
    [InlineData("someone@example.com", false)]
    public void TryValidateAccessIdentity_Identity_ReportsSyntax(string identity, bool expected)
    {
        var provider = CreateProvider(keyName: null);

        var valid = provider.TryValidateAccessIdentity(identity, out var failure);

        Assert.Equal(expected, valid);
        Assert.Equal(expected, failure is null);
        if (!expected)
            Assert.StartsWith(
                "must be the email address of a Google Cloud service account",
                failure
            );
    }

    #endregion
}
