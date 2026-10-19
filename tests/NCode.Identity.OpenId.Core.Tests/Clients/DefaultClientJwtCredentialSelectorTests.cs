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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientJwtCredentialSelectorTests : BaseTests, IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _tenantRsa = RSA.Create(2048);
    private readonly RSA _clientRsa = RSA.Create(2048);

    public DefaultClientJwtCredentialSelectorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        TenantSigningKey = secretKeyFactory.CreateRsa(
            new KeyMetadata { KeyId = "tenant-sig", Use = SecretKeyUses.Signature },
            _tenantRsa
        );
        ClientEncryptionKey = secretKeyFactory.CreateRsaPublicKey(
            new KeyMetadata { KeyId = "client-enc", Use = SecretKeyUses.Encryption },
            _clientRsa
        );
        ClientSharedSecret = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "client-secret" },
            new byte[32]
        );

        MockClientKeyResolver = CreateStrictMock<IClientKeyResolver>();
        Selector = new DefaultClientJwtCredentialSelector(
            _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            _serviceProvider.GetRequiredService<ICredentialSelector>(),
            MockClientKeyResolver.Object
        );
    }

    private SecretKey TenantSigningKey { get; }
    private SecretKey ClientEncryptionKey { get; }
    private SecretKey ClientSharedSecret { get; }
    private Mock<IClientKeyResolver> MockClientKeyResolver { get; }
    private DefaultClientJwtCredentialSelector Selector { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _tenantRsa.Dispose();
        _clientRsa.Dispose();
    }

    #region Scaffolding

    private ISecretKeyCollection CreateKeys(params SecretKey[] keys) =>
        _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys);

    private OpenIdContext CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSecretsProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockTenant.SetupGet(x => x.SecretsProvider).Returns(mockSecretsProvider.Object);
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(CreateKeys(TenantSigningKey));
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        return mockContext.Object;
    }

    private OpenIdClient CreateClient(IReadOnlyDictionary<string, object> settings)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(new StubSettings(settings));
        return mockClient.Object;
    }

    private void SetupClientKeys(OpenIdClient client, params SecretKey[] keys) =>
        MockClientKeyResolver
            .Setup(x => x.ResolveSecretKeysAsync(client, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKeys(keys))
            .Verifiable();

    private async Task<ClientJwtCredentials> SelectAsync(
        OpenIdClient client,
        ClientJwtProtectionSettings protectionSettings
    ) =>
        await Selector.SelectCredentialsAsync(
            CreateContext(),
            client,
            protectionSettings,
            CancellationToken.None
        );

    #endregion

    #region SelectCredentialsAsync Tests

    [Fact]
    public async Task SelectCredentialsAsync_WhenUserInfoRegistersNothing_ReturnsUnprotected()
    {
        var client = CreateClient(new Dictionary<string, object>());

        var credentials = await SelectAsync(client, ClientJwtProtectionSettings.UserInfo);

        Assert.False(credentials.IsProtected);
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenSignatureRegistered_SignsWithTheTenantKey()
    {
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoSignedResponseAlg] = "RS256",
                [OpenIdSettingNames.UserInfoSigningAlgValuesSupported] =
                    (IReadOnlyCollection<string>)["RS256", "PS256"],
            }
        );

        var credentials = await SelectAsync(client, ClientJwtProtectionSettings.UserInfo);

        var signingCredentials = Assert.IsType<JoseSigningCredentials>(
            credentials.SigningCredentials
        );
        Assert.Same(TenantSigningKey, signingCredentials.SecretKey);
        Assert.Equal("RS256", signingCredentials.SignatureAlgorithm.Code);
        Assert.Null(credentials.EncryptionCredentials);
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenRegisteredSignatureNotPermitted_Throws()
    {
        // A security profile's ceiling binds the client's registration; it is never weakened to fit (ADR-0155).
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.IdTokenSignedResponseAlg] = "RS256",
                [OpenIdSettingNames.IdTokenSigningAlgValuesSupported] =
                    (IReadOnlyCollection<string>)["PS256", "ES256"],
            }
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            SelectAsync(client, ClientJwtProtectionSettings.IdToken)
        );
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenTenantHasNoKeyForTheRegisteredSignature_Throws()
    {
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.IdTokenSignedResponseAlg] = "ES256",
            }
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            SelectAsync(client, ClientJwtProtectionSettings.IdToken)
        );
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenEncryptionRegistered_EncryptsToTheClientPublicKeyWithDefaultEnc()
    {
        // The client's shared secret is not an encryption key it registered, so only its public key is used; an
        // unregistered enc is A128CBC-HS256 and nothing is compressed.
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.IdTokenSignedResponseAlg] = "RS256",
                [OpenIdSettingNames.IdTokenEncryptedResponseAlg] = "RSA-OAEP-256",
            }
        );
        SetupClientKeys(client, ClientSharedSecret, ClientEncryptionKey);

        var credentials = await SelectAsync(client, ClientJwtProtectionSettings.IdToken);

        var encryptionCredentials = Assert.IsType<JoseEncryptionCredentials>(
            credentials.EncryptionCredentials
        );
        Assert.Same(ClientEncryptionKey, encryptionCredentials.SecretKey);
        Assert.Equal("RSA-OAEP-256", encryptionCredentials.KeyManagementAlgorithm.Code);
        Assert.Equal("A128CBC-HS256", encryptionCredentials.AuthenticatedEncryptionAlgorithm.Code);
        Assert.Null(encryptionCredentials.CompressionAlgorithm);
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenRegisteredEncNotPermitted_Throws()
    {
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoEncryptedResponseAlg] = "RSA-OAEP-256",
                [OpenIdSettingNames.UserInfoEncryptedResponseEnc] = "A128GCM",
                [OpenIdSettingNames.UserInfoEncryptionEncValuesSupported] =
                    (IReadOnlyCollection<string>)["A256GCM"],
            }
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            SelectAsync(client, ClientJwtProtectionSettings.UserInfo)
        );
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenClientHasOnlyASharedSecret_Throws()
    {
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoEncryptedResponseAlg] = "RSA-OAEP-256",
            }
        );
        SetupClientKeys(client, ClientSharedSecret);

        await Assert.ThrowsAsync<OpenIdException>(() =>
            SelectAsync(client, ClientJwtProtectionSettings.UserInfo)
        );
    }

    [Fact]
    public async Task SelectCredentialsAsync_WhenEncryptionRequiredButNotRegistered_Throws()
    {
        // A policy that requires encryption never guesses an algorithm the client did not register.
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.IdTokenSignedResponseAlg] = "RS256",
                [OpenIdSettingNames.IdTokenEncryptionRequired] = true,
            }
        );

        await Assert.ThrowsAsync<OpenIdException>(() =>
            SelectAsync(client, ClientJwtProtectionSettings.IdToken)
        );
    }

    #endregion

    #region Scaffolding Types

    private sealed class StubSettings(IReadOnlyDictionary<string, object> values)
        : IReadOnlySettingCollection
    {
        public int Count => values.Count;

        public bool TryGetValue<TValue>(
            SettingKey<TValue> key,
            [MaybeNullWhen(false)] out TValue value
        )
            where TValue : notnull
        {
            if (values.TryGetValue(key.SettingName, out var raw) && raw is TValue typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public TValue GetValue<TValue>(SettingKey<TValue> key)
            where TValue : notnull =>
            TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();

        public bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting) =>
            throw new NotSupportedException();

        public bool TryGet<TValue>(
            SettingKey<TValue> key,
            [MaybeNullWhen(false)] out Setting<TValue> setting
        )
            where TValue : notnull => throw new NotSupportedException();

        public bool TryGetValue(string settingName, [MaybeNullWhen(false)] out object value) =>
            throw new NotSupportedException();

        public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
            throw new NotSupportedException();

        public IEnumerator<Setting> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
