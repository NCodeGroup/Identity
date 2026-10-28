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
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Client.Tokens;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

public class ClientDecryptionPolicyTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";

    private ServiceProvider ServiceProvider { get; }

    public ClientDecryptionPolicyTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    private IAlgorithmCollection Algorithms =>
        ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    private static ClientRegistration CreateRegistration(
        IReadOnlyList<string>? keyManagement = null,
        IReadOnlyList<string>? contentEncryption = null,
        string? keyRing = null
    ) =>
        new(Issuer, ClientId, OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt)
        {
            IdTokenKeyManagementAlgorithms = keyManagement,
            IdTokenContentEncryptionAlgorithms = contentEncryption,
            KeyRing = keyRing,
        };

    #region IsEncryptionRegistered Tests

    [Fact]
    public void IsEncryptionRegistered_WhenNotSet_ReturnsFalse()
    {
        var registration = CreateRegistration();

        var result = ClientDecryptionPolicy.IsEncryptionRegistered(registration);

        Assert.False(result);
    }

    [Fact]
    public void IsEncryptionRegistered_WhenEmpty_ReturnsFalse()
    {
        var registration = CreateRegistration(keyManagement: []);

        var result = ClientDecryptionPolicy.IsEncryptionRegistered(registration);

        Assert.False(result);
    }

    [Fact]
    public void IsEncryptionRegistered_WhenSet_ReturnsTrue()
    {
        var registration = CreateRegistration(keyManagement: ["RSA-OAEP-256"]);

        var result = ClientDecryptionPolicy.IsEncryptionRegistered(registration);

        Assert.True(result);
    }

    #endregion

    #region GetPermittedKeyManagementAlgorithms Tests

    [Fact]
    public void GetPermittedKeyManagementAlgorithms_WhenNotRegistered_ReturnsEmpty()
    {
        var registration = CreateRegistration();

        var result = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            Algorithms,
            registration
        );

        Assert.Empty(result);
    }

    [Fact]
    public void GetPermittedKeyManagementAlgorithms_WhenAsymmetric_ReturnsThemInOrder()
    {
        var registration = CreateRegistration(keyManagement: ["ECDH-ES+A128KW", "RSA-OAEP-256"]);

        var result = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            Algorithms,
            registration
        );

        Assert.Equal(["ECDH-ES+A128KW", "RSA-OAEP-256"], result);
    }

    [Fact]
    public void GetPermittedKeyManagementAlgorithms_WhenSymmetricOrUnknown_ExcludesThem()
    {
        var registration = CreateRegistration(
            keyManagement: ["A128KW", "dir", "PBES2-HS256+A128KW", "X-UNKNOWN", "RSA-OAEP"]
        );

        var result = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            Algorithms,
            registration
        );

        Assert.Equal(["RSA-OAEP"], result);
    }

    [Fact]
    public void GetPermittedKeyManagementAlgorithms_WhenRsa15IsRegistered_ExcludesIt()
    {
        var mockRsa15 = CreateStrictMock<KeyManagementAlgorithm>();
        mockRsa15.Setup(x => x.KeyType).Returns(typeof(RsaSecretKey)).Verifiable();

        var mockRsaOaep256 = CreateStrictMock<KeyManagementAlgorithm>();
        mockRsaOaep256.Setup(x => x.KeyType).Returns(typeof(RsaSecretKey)).Verifiable();

        var rsa15 = mockRsa15.Object;
        var rsaOaep256 = mockRsaOaep256.Object;

        var mockAlgorithms = CreateStrictMock<IAlgorithmCollection>();
        mockAlgorithms
            .Setup(x => x.TryGetKeyManagementAlgorithm("RSA1_5", out rsa15))
            .Returns(true)
            .Verifiable();
        mockAlgorithms
            .Setup(x => x.TryGetKeyManagementAlgorithm("RSA-OAEP-256", out rsaOaep256))
            .Returns(true)
            .Verifiable();

        var registration = CreateRegistration(keyManagement: ["RSA1_5", "RSA-OAEP-256"]);

        var result = ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms(
            mockAlgorithms.Object,
            registration
        );

        Assert.Equal(["RSA-OAEP-256"], result);
    }

    #endregion

    #region GetPermittedContentEncryptionAlgorithms Tests

    [Fact]
    public void GetPermittedContentEncryptionAlgorithms_WhenNotRegistered_ReturnsEmpty()
    {
        var registration = CreateRegistration();

        var result = ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(
            Algorithms,
            registration
        );

        Assert.Empty(result);
    }

    [Fact]
    public void GetPermittedContentEncryptionAlgorithms_WhenOnlyContentRegistered_Throws()
    {
        var registration = CreateRegistration(contentEncryption: ["A256GCM"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(Algorithms, registration)
        );

        Assert.Contains(ClientId, exception.Message);
    }

    [Fact]
    public void GetPermittedContentEncryptionAlgorithms_WhenNotListed_ReturnsRegistrationDefault()
    {
        var registration = CreateRegistration(keyManagement: ["RSA-OAEP-256"]);

        var result = ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(
            Algorithms,
            registration
        );

        Assert.Equal(["A128CBC-HS256"], result);
    }

    [Fact]
    public void GetPermittedContentEncryptionAlgorithms_WhenListed_ReturnsKnownOnes()
    {
        var registration = CreateRegistration(
            keyManagement: ["RSA-OAEP-256"],
            contentEncryption: ["A256GCM", "X-UNKNOWN", "A128CBC-HS256"]
        );

        var result = ClientDecryptionPolicy.GetPermittedContentEncryptionAlgorithms(
            Algorithms,
            registration
        );

        Assert.Equal(["A256GCM", "A128CBC-HS256"], result);
    }

    #endregion

    #region GetDecryptionKeys Tests

    [Fact]
    public void GetDecryptionKeys_WhenNoKeyRing_Throws()
    {
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        var registration = CreateRegistration(keyManagement: ["RSA-OAEP-256"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ClientDecryptionPolicy.GetDecryptionKeys(mockKeyRingProvider.Object, registration)
        );

        Assert.Contains(ClientId, exception.Message);
    }

    [Fact]
    public void GetDecryptionKeys_WhenKeyRing_ReturnsOnlyAsymmetricKeys()
    {
        const string keyRing = "ring-1";
        using var rsa = RSA.Create(2048);
        var keyFactory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        var rsaKey = keyFactory.CreateRsa(
            new KeyMetadata { KeyId = "r1", Use = SecretKeyUses.Encryption },
            rsa
        );
        var symmetricKey = keyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "s1" },
            "secret".AsSpan()
        );
        var keys = ServiceProvider
            .GetRequiredService<ISecretKeyCollectionFactory>()
            .Create([rsaKey, symmetricKey]);
        var mockKeyRingProvider = CreateStrictMock<IClientKeyRingProvider>();
        mockKeyRingProvider.Setup(x => x.GetKeyRing(keyRing)).Returns(keys).Verifiable();
        var registration = CreateRegistration(keyManagement: ["RSA-OAEP-256"], keyRing: keyRing);

        var result = ClientDecryptionPolicy.GetDecryptionKeys(
            mockKeyRingProvider.Object,
            registration
        );

        var key = Assert.Single(result);
        Assert.Same(rsaKey, key);
    }

    #endregion

    #region IsReplicatedIssuerValid Tests

    [Fact]
    public void IsReplicatedIssuerValid_WhenNotEncrypted_ReturnsTrue()
    {
        var result = ClientDecryptionPolicy.IsReplicatedIssuerValid(null, Issuer);

        Assert.True(result);
    }

    [Fact]
    public void IsReplicatedIssuerValid_WhenHeaderHasNoIssuer_ReturnsTrue()
    {
        var header = JsonElement.Parse("""{"alg":"RSA-OAEP-256","enc":"A256GCM"}""");

        var result = ClientDecryptionPolicy.IsReplicatedIssuerValid(header, Issuer);

        Assert.True(result);
    }

    [Fact]
    public void IsReplicatedIssuerValid_WhenIssuerMatches_ReturnsTrue()
    {
        var header = JsonElement.Parse(
            """{"alg":"RSA-OAEP-256","iss":"https://op.example.test"}"""
        );

        var result = ClientDecryptionPolicy.IsReplicatedIssuerValid(header, Issuer);

        Assert.True(result);
    }

    [Fact]
    public void IsReplicatedIssuerValid_WhenIssuerDiffers_ReturnsFalse()
    {
        var header = JsonElement.Parse(
            """{"alg":"RSA-OAEP-256","iss":"https://evil.example.test"}"""
        );

        var result = ClientDecryptionPolicy.IsReplicatedIssuerValid(header, Issuer);

        Assert.False(result);
    }

    [Fact]
    public void IsReplicatedIssuerValid_WhenIssuerIsNotAString_ReturnsFalse()
    {
        var header = JsonElement.Parse("""{"alg":"RSA-OAEP-256","iss":42}""");

        var result = ClientDecryptionPolicy.IsReplicatedIssuerValid(header, Issuer);

        Assert.False(result);
    }

    #endregion
}
