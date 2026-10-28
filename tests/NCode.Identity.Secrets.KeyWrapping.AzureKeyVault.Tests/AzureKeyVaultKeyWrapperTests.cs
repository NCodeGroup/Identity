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

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests;

public class AzureKeyVaultKeyWrapperTests : IDisposable
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private Mock<IAzureKeyVaultClientFactory> ClientFactory { get; } = new(MockBehavior.Strict);

    private Mock<CryptographyClient> Client { get; } = new(MockBehavior.Strict);

    private byte[]? SentArray { get; set; }

    private byte[]? SentKey { get; set; }

    private byte[]? SentCiphertext { get; set; }

    public void Dispose()
    {
        ClientFactory.Verify();
        Client.Verify();
    }

    private AzureKeyVaultKeyWrapper CreateProvider(string? keyId) =>
        new(
            ClientFactory.Object,
            Microsoft.Extensions.Options.Options.Create(
                new AzureKeyVaultKeyWrapperOptions { KeyId = keyId }
            )
        );

    private static WrapResult CreateWrapResult(string keyId, byte[] encryptedKey) =>
        CryptographyModelFactory.WrapResult(keyId, encryptedKey, KeyWrapAlgorithm.RsaOaep256);

    private void SetupClient(string keyId) =>
        ClientFactory.Setup(x => x.GetClient(keyId)).Returns(Client.Object).Verifiable();

    // The provider zeroes the array it sends once the call returns, so the test keeps a copy of what was sent along with
    // the array itself.
    private void SetupWrapKey(WrapResult result, CancellationToken cancellationToken) =>
        Client
            .Setup(x =>
                x.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, It.IsAny<byte[]>(), cancellationToken)
            )
            .Callback<KeyWrapAlgorithm, byte[], CancellationToken>(
                (_, key, _) =>
                {
                    SentArray = key;
                    SentKey = key.ToArray();
                }
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static UnwrapResult CreateUnwrapResult(string keyId, byte[] key) =>
        CryptographyModelFactory.UnwrapResult(keyId, key, KeyWrapAlgorithm.RsaOaep256);

    private static WrappedKey CreateWrappedKey(string keyId, byte[] ciphertext) =>
        new()
        {
            WrapperName = KeyWrapperNames.AzureKeyVault,
            KeyId = keyId,
            Ciphertext = Base64Url.EncodeToString(ciphertext),
        };

    private void SetupUnwrapKey(UnwrapResult result, CancellationToken cancellationToken) =>
        Client
            .Setup(x =>
                x.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, It.IsAny<byte[]>(), cancellationToken)
            )
            .Callback<KeyWrapAlgorithm, byte[], CancellationToken>(
                (_, encryptedKey, _) => SentCiphertext = encryptedKey.ToArray()
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupUnwrapKeyThrows(Exception exception) =>
        Client
            .Setup(x =>
                x.UnwrapKeyAsync(
                    KeyWrapAlgorithm.RsaOaep256,
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception)
            .Verifiable();

    #region WrapAsync Tests

    [Fact]
    public async Task WrapAsync_ServiceWrapsKey_SendsKeyWithRsaOaep256AndReturnsWrappedKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        using var cancellationSource = new CancellationTokenSource();
        SetupClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), cancellationSource.Token);
        var provider = CreateProvider(KeyId);

        var wrappedKey = await provider.WrapAsync(key, context, cancellationSource.Token);

        Assert.Equal(key, SentKey);
        Assert.Equal(KeyWrapperNames.AzureKeyVault, wrappedKey.WrapperName);
        Assert.Equal(KeyVersionId, wrappedKey.KeyId);
        Assert.Equal(Base64Url.EncodeToString(encryptedKey), wrappedKey.Ciphertext);
    }

    [Fact]
    public async Task WrapAsync_ServiceWrapsKey_ZeroesArraySentToService()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await provider.WrapAsync(key, context, CancellationToken.None);

        var sentArray = Assert.IsType<byte[]>(SentArray);
        Assert.Equal(new byte[key.Length], sentArray);
    }

    [Fact]
    public async Task WrapAsync_KeyIdUnset_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var provider = CreateProvider(keyId: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_ResultKeyIdWithoutVersion_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(384);
        SetupClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task WrapAsync_WrappedKeyShorterThan3072Bits_ThrowsInvalidOperationException()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var context = "root-context"u8.ToArray();
        var encryptedKey = RandomNumberGenerator.GetBytes(256);
        SetupClient(KeyId);
        SetupWrapKey(CreateWrapResult(KeyVersionId, encryptedKey), CancellationToken.None);
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.WrapAsync(key, context, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region UnwrapAsync Tests

    [Fact]
    public async Task UnwrapAsync_SendsRecordedKeyVersionWithRsaOaep256_WritesPlaintextAndZeroesResultKey()
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var result = CreateUnwrapResult(KeyVersionId, plaintext.ToArray());
        using var cancellationSource = new CancellationTokenSource();
        SetupClient(KeyVersionId);
        SetupUnwrapKey(result, cancellationSource.Token);
        var provider = CreateProvider(KeyId);

        await provider.UnwrapAsync(wrappedKey, context, destination, cancellationSource.Token);

        Assert.Equal(ciphertext, SentCiphertext);
        Assert.Equal(plaintext.Length, destination.WrittenCount);
        Assert.Equal(plaintext, destination.WrittenSpan.ToArray());
        Assert.Equal(new byte[plaintext.Length], result.Key);
    }

    [Fact]
    public async Task UnwrapAsync_ServiceRejectsCiphertext_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var serviceException = new RequestFailedException(400, "x");
        SetupClient(KeyVersionId);
        SetupUnwrapKeyThrows(serviceException);
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(serviceException, exception.InnerException);
    }

    [Fact]
    public async Task UnwrapAsync_ServiceUnavailable_Propagates()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyVersionId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var serviceException = new RequestFailedException(503, "x");
        SetupClient(KeyVersionId);
        SetupUnwrapKeyThrows(serviceException);
        var provider = CreateProvider(KeyId);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );

        Assert.Same(serviceException, exception);
    }

    [Fact]
    public async Task UnwrapAsync_KeyIdWithoutVersion_ThrowsCryptographicException()
    {
        var ciphertext = RandomNumberGenerator.GetBytes(384);
        var context = "root-context"u8.ToArray();
        var wrappedKey = CreateWrappedKey(KeyId, ciphertext);
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyId);

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
            WrapperName = KeyWrapperNames.AzureKeyVault,
            KeyId = KeyVersionId,
            Ciphertext = ciphertext,
        };
        var destination = new ArrayBufferWriter<byte>();
        var provider = CreateProvider(KeyId);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
