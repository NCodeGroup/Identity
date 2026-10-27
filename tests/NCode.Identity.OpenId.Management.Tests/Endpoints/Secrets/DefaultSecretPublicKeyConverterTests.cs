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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

// See ADR-0235.
public sealed class DefaultSecretPublicKeyConverterTests : IAsyncLifetime
{
    private const string KeyId = "kid-1";

    private MockRepository MockRepository { get; }
    private Mock<ISecretSerializer> MockSecretSerializer { get; }
    private ServiceProvider ServiceProvider { get; }
    private ISecretKeyFactory SecretKeyFactory { get; }
    private DefaultSecretPublicKeyConverter Converter { get; }

    public DefaultSecretPublicKeyConverterTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockSecretSerializer = MockRepository.Create<ISecretSerializer>();

        // Resolve the real key factory and key-type catalog so the converter exports through the same descriptors
        // the server registers; only the serializer is mocked.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        SecretKeyFactory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();

        Converter = new DefaultSecretPublicKeyConverter(
            MockSecretSerializer.Object,
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        MockRepository.Verify();
        await ServiceProvider.DisposeAsync();
    }

    private static PersistedSecret CreatePersistedSecret(
        string secretType,
        string secretEncoding,
        int keySizeBits,
        string? publicKey = null
    ) =>
        new()
        {
            SecretId = "secret-1",
            KeyId = KeyId,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            KeySizeBits = keySizeBits,
            EncodedValue = "encoded",
            PublicKey = publicKey,
        };

    private static KeyMetadata CreateMetadata() =>
        new()
        {
            KeyId = KeyId,
            Use = "sig",
            Algorithm = "RS256",
        };

    #region Convert Tests

    // See ADR-0268.
    [Fact]
    public void Convert_PrivateKeyWithStoredPublicKey_ReturnsPublicJwk()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var secretKey = SecretKeyFactory.CreateRsa(CreateMetadata(), rsa);
        var secret = CreatePersistedSecret(
            SecretTypes.Rsa,
            SecretEncodings.Pkcs8,
            2048,
            publicKey: "spki"
        );

        MockSecretSerializer
            .Setup(x => x.DeserializePublicSecret(secret))
            .Returns(secretKey)
            .Verifiable();

        var resource = Converter.Convert(secret);

        Assert.NotNull(resource);
        Assert.Equal(secret.SecretId, resource.SecretId);
        Assert.Equal(secret.SecretType, resource.SecretType);
        Assert.Equal("RSA", resource.Jwk.GetProperty("kty").GetString());
        Assert.Equal(KeyId, resource.Jwk.GetProperty("kid").GetString());
        Assert.Equal(
            Base64Url.EncodeToString(parameters.Modulus!),
            resource.Jwk.GetProperty("n").GetString()
        );
        Assert.Equal(
            Base64Url.EncodeToString(parameters.Exponent!),
            resource.Jwk.GetProperty("e").GetString()
        );
        Assert.False(resource.Jwk.TryGetProperty("d", out _));
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", resource.Pem, StringComparison.Ordinal);
    }

    // See ADR-0268.
    [Fact]
    public void Convert_PrivateKeyWithoutStoredPublicKey_ReturnsNull()
    {
        var secret = CreatePersistedSecret(SecretTypes.Rsa, SecretEncodings.Pkcs8, 2048);

        var resource = Converter.Convert(secret);

        Assert.Null(resource);
    }

    // See ADR-0268.
    [Fact]
    public void Convert_SymmetricSecret_ReturnsNullWithoutDeserializing()
    {
        var secret = CreatePersistedSecret(
            SecretTypes.Symmetric,
            SecretEncodings.SymmetricRaw,
            256
        );

        var resource = Converter.Convert(secret);

        Assert.Null(resource);
    }

    #endregion
}
