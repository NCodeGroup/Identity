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

using System.Buffers.Text;
using System.Security.Cryptography;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Tests;

public class GoogleCloudKmsRootKeyProviderTests : IDisposable
{
    private const string CryptoKeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private const string CryptoKeyVersionName = CryptoKeyName + "/cryptoKeyVersions/3";

    private const string OtherCryptoKeyVersionName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/other/cryptoKeyVersions/3";

    private Mock<IGoogleCloudKmsClientFactory> ClientFactory { get; } = new(MockBehavior.Strict);

    private Mock<KeyManagementServiceClient> Client { get; } = new(MockBehavior.Strict);

    private CapturedRequest? EncryptCall { get; set; }

    private CapturedRequest? DecryptCall { get; set; }

    public void Dispose()
    {
        ClientFactory.Verify();
        Client.Verify();
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

    private GoogleCloudKmsRootKeyProvider CreateProvider(string? keyName) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new GoogleCloudKmsRootKeyOptions { KeyName = keyName }
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

    private static RootWrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            ProviderName = RootKeyProviderNames.GoogleCloudKms,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private static RpcException CreateRpcException(StatusCode statusCode) =>
        new(new Status(statusCode, "x"));

    private void SetupClient(CancellationToken cancellationToken) =>
        ClientFactory
            .Setup(x => x.GetClientAsync(cancellationToken))
            .ReturnsAsync(Client.Object)
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

    private void SetupDecryptThrows(RpcException exception, Times times) =>
        Client
            .Setup(x => x.DecryptAsync(It.IsAny<DecryptRequest>(), CancellationToken.None))
            .ThrowsAsync(exception)
            .Verifiable(times);

    #region WrapAsync Tests

    [Fact]
    public async Task WrapAsync_ServiceConfirmsIntegrity_SendsChecksummedRequestAndReturnsWrappedKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        using var cancellationSource = new CancellationTokenSource();
        SetupClient(cancellationSource.Token);
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
        Assert.Equal(RootKeyProviderNames.GoogleCloudKms, wrappedKey.ProviderName);
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

        SetupClient(CancellationToken.None);
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
        SetupClient(CancellationToken.None);
        SetupEncrypt(CreateEncryptResponse(responseName, ciphertext), CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region UnwrapAsync Tests

    [Fact]
    public async Task UnwrapAsync_SendsCryptoKeyNameAndChecksums_WritesPlaintextToDestination()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new byte[48];
        var response = CreateDecryptResponse(plaintext);
        using var cancellationSource = new CancellationTokenSource();
        SetupClient(cancellationSource.Token);
        SetupDecrypt(response, cancellationSource.Token);
        var provider = CreateProvider(CryptoKeyName);

        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            cancellationSource.Token
        );

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(CryptoKeyName, request.Name);
        Assert.Equal(ciphertext, request.Data);
        Assert.Equal(context, request.AdditionalAuthenticatedData);
        Assert.Equal(GoogleCloudKmsChecksum.Compute(ciphertext), request.DataCrc32C);
        Assert.Equal(
            GoogleCloudKmsChecksum.Compute(context),
            request.AdditionalAuthenticatedDataCrc32C
        );
        Assert.Equal(plaintext.Length, written);
        Assert.Equal(plaintext, destination[..written]);
        Assert.Equal(new byte[destination.Length - written], destination[written..]);
        Assert.Equal(new byte[plaintext.Length], response.Plaintext.ToByteArray());
    }

    [Fact]
    public async Task UnwrapAsync_DestinationTooSmall_ThrowsArgumentException()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new byte[16];
        SetupClient(CancellationToken.None);
        SetupDecrypt(CreateDecryptResponse(plaintext), CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public async Task UnwrapAsync_PlaintextChecksumMismatch_ThrowsIOException()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new byte[32];
        var response = CreateDecryptResponse(plaintext);
        response.PlaintextCrc32C ^= 1;
        SetupClient(CancellationToken.None);
        SetupDecrypt(response, CancellationToken.None);
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Equal(new byte[destination.Length], destination);
        Assert.Equal(new byte[plaintext.Length], response.Plaintext.ToByteArray());
    }

    [Fact]
    public async Task UnwrapAsync_InvalidArgumentThenSuccess_RetriesAndWritesPlaintext()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new byte[32];
        var calls = 0;
        SetupClient(CancellationToken.None);
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

        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(plaintext.Length, written);
        Assert.Equal(plaintext, destination);
    }

    [Fact]
    public async Task UnwrapAsync_InvalidArgumentTwice_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(CryptoKeyVersionName, ciphertext);
        var destination = new byte[32];
        var rpcException = CreateRpcException(StatusCode.InvalidArgument);
        SetupClient(CancellationToken.None);
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
        var destination = new byte[32];
        var rpcException = CreateRpcException(StatusCode.Unavailable);
        SetupClient(CancellationToken.None);
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
        var destination = new byte[32];
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
        var wrappedKey = new RootWrappedKey
        {
            ProviderName = RootKeyProviderNames.GoogleCloudKms,
            KeyId = CryptoKeyVersionName,
            Ciphertext = ciphertext,
        };
        var destination = new byte[32];
        var provider = CreateProvider(CryptoKeyName);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
