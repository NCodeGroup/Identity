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
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Moq;
using NCode.Identity.Secrets.Persistence.AwsKms.Facades;
using NCode.Identity.Secrets.Persistence.AwsKms.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.Secrets.Persistence.AwsKms.Tests;

public class AwsKmsRootKeyProviderTests : IDisposable
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private const string AliasArn = "arn:aws:kms:us-west-2:111122223333:alias/ncode-identity-root";

    private const string OtherRegionKeyArn =
        "arn:aws:kms:eu-central-1:111122223333:key/abcd1234-34cd-12ab-56ef-0987654321ba";

    private const string ExpectedContextKey = "ncode-identity:root-context";

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

    private AwsKmsRootKeyProvider CreateProvider(string? keyArn) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new AwsKmsRootKeyOptions { KeyArn = keyArn }
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

    private static RootWrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            ProviderName = RootKeyProviderNames.AwsKms,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private void SetupClient(string region) =>
        ClientFactory.Setup(x => x.GetClient(region)).Returns(Client.Object).Verifiable();

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
        Assert.Equal(RootKeyProviderNames.AwsKms, wrappedKey.ProviderName);
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
        var destination = new byte[48];
        using var cancellationSource = new CancellationTokenSource();
        SetupClient("eu-central-1");
        SetupDecrypt(
            new DecryptResponse { Plaintext = CreateStream(plaintext, plaintextBufferIsVisible) },
            cancellationSource.Token
        );
        var provider = CreateProvider(KeyArn);

        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            cancellationSource.Token
        );

        var request = Assert.IsType<CapturedRequest>(DecryptCall);
        Assert.Equal(OtherRegionKeyArn, request.KeyId);
        Assert.Equal(ciphertext, request.Data);
        Assert.Equal(EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT, request.Algorithm);
        var contextEntry = Assert.Single(request.EncryptionContext);
        Assert.Equal(ExpectedContextKey, contextEntry.Key);
        Assert.Equal(Base64Url.EncodeToString(context), contextEntry.Value);
        Assert.Equal(plaintext.Length, written);
        Assert.Equal(plaintext, destination[..written]);
        Assert.Equal(new byte[destination.Length - written], destination[written..]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnwrapAsync_DestinationTooSmall_ThrowsArgumentException(
        bool plaintextBufferIsVisible
    )
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyArn, ciphertext);
        var destination = new byte[16];
        SetupClient("us-west-2");
        SetupDecrypt(
            new DecryptResponse { Plaintext = CreateStream(plaintext, plaintextBufferIsVisible) },
            CancellationToken.None
        );
        var provider = CreateProvider(KeyArn);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public async Task UnwrapAsync_InvalidCiphertext_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(64);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyArn, ciphertext);
        var destination = new byte[32];
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
        var destination = new byte[32];
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
        var destination = new byte[32];
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
        var destination = new byte[32];
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
        var wrappedKey = new RootWrappedKey
        {
            ProviderName = RootKeyProviderNames.AwsKms,
            KeyId = KeyArn,
            Ciphertext = ciphertext,
        };
        var destination = new byte[32];
        var provider = CreateProvider(KeyArn);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
