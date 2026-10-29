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

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests;

/// <summary>
/// Proves that a failed key unwrap is indistinguishable from a failed authentication tag (RFC 7516 §11.5): both fail
/// with the same exception type and message, and neither carries an inner exception.
/// </summary>
public sealed class JoseSerializerJweDecryptionTests : IAsyncLifetime, IDisposable
{
    private const string Payload = "the payload";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly ServiceProvider _serviceProvider;

    public JoseSerializerJweDecryptionTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary();
        _serviceProvider = services.BuildServiceProvider();
    }

    private IJoseSerializer JoseSerializer =>
        _serviceProvider.GetRequiredService<IJoseSerializer>();

    private ISecretKeyFactory SecretKeyFactory =>
        _serviceProvider.GetRequiredService<ISecretKeyFactory>();

    private IAlgorithmCollection Algorithms =>
        _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _serviceProvider.DisposeAsync();

    public void Dispose() => _rsa.Dispose();

    #region Scaffolding

    private SecretKey CreateRsaKey() =>
        SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, _rsa);

    private SecretKey CreateSymmetricKey(int bitCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(bitCount / 8);
        return SecretKeyFactory.CreateSymmetric(new KeyMetadata { KeyId = "sym" }, bytes);
    }

    private string Encrypt(SecretKey secretKey, string keyManagementCode)
    {
        Assert.True(
            Algorithms.TryGetKeyManagementAlgorithm(keyManagementCode, out var keyManagement)
        );
        Assert.True(
            Algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                out var contentEncryption
            )
        );
        var credentials = new JoseEncryptionCredentials(
            secretKey,
            keyManagement,
            contentEncryption,
            null
        );
        return JoseSerializer.CreateEncoder(new JoseEncryptionOptions(credentials)).Encode(Payload);
    }

    // Replaces one segment of a compact JWE: 1 is the encrypted key, 4 the authentication tag.
    private static string ReplaceSegment(string token, int index, Func<byte[], byte[]> change)
    {
        var segments = token.Split('.');
        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(segments[index]);
        segments[index] = System.Buffers.Text.Base64Url.EncodeToString(change(bytes));
        return string.Join('.', segments);
    }

    private static byte[] FlipFirstBit(byte[] bytes)
    {
        var copy = bytes.ToArray();
        copy[0] ^= 0x01;
        return copy;
    }

    private void AssertUniformFailure(string token, SecretKey secretKey)
    {
        var exception = Assert.Throws<JoseEncryptionException>(() =>
            JoseSerializer.Decode(token, secretKey)
        );

        Assert.Equal("Failed to decrypt the JWE.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    #endregion

    #region Decode Tests

    [Fact]
    public void Decode_WhenTheJweIsIntact_ReturnsThePayload()
    {
        var secretKey = CreateRsaKey();
        var token = Encrypt(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        Assert.Equal(Payload, JoseSerializer.Decode(token, secretKey));
    }

    [Fact]
    public void Decode_WhenTheRsaEncryptedKeyIsTampered_FailsLikeATamperedTag()
    {
        var secretKey = CreateRsaKey();
        var token = Encrypt(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        AssertUniformFailure(ReplaceSegment(token, 1, FlipFirstBit), secretKey);
        AssertUniformFailure(ReplaceSegment(token, 4, FlipFirstBit), secretKey);
    }

    [Fact]
    public void Decode_WhenTheAesWrappedKeyFailsItsIntegrityCheck_FailsLikeATamperedTag()
    {
        var secretKey = CreateSymmetricKey(128);
        var token = Encrypt(secretKey, AlgorithmCodes.KeyManagement.Aes128);

        AssertUniformFailure(ReplaceSegment(token, 1, FlipFirstBit), secretKey);
        AssertUniformFailure(ReplaceSegment(token, 4, FlipFirstBit), secretKey);
    }

    [Fact]
    public void Decode_WhenTheEncryptedKeyHasTheWrongLength_FailsLikeATamperedTag()
    {
        var secretKey = CreateSymmetricKey(128);
        var token = Encrypt(secretKey, AlgorithmCodes.KeyManagement.Aes128);

        AssertUniformFailure(ReplaceSegment(token, 1, bytes => bytes[..^8]), secretKey);
    }

    [Fact]
    public void Decode_WhenTheKeyDoesNotFitTheAlgorithm_ReportsTheConfigurationError()
    {
        var secretKey = CreateRsaKey();
        var token = Encrypt(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        Assert.Throws<ArgumentException>(() =>
            JoseSerializer.Decode(token, CreateSymmetricKey(128))
        );
    }

    #endregion
}
