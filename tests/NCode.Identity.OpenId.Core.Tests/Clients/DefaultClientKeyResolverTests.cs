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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientKeyResolverTests : BaseTests, IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _rsa = RSA.Create(2048);

    public DefaultClientKeyResolverTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        StoredPublicKey = secretKeyFactory.CreateRsaPublicKey(
            new KeyMetadata { KeyId = "stored-public-key", Use = SecretKeyUses.Signature },
            _rsa
        );
        SharedSecret = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "shared-secret" },
            new byte[32]
        );

        // The jwks_uri cases here are refused before any fetch, so the fetch collaborators are never called.
        Resolver = new DefaultClientKeyResolver(
            _serviceProvider.GetRequiredService<ILogger<DefaultClientKeyResolver>>(),
            TimeProvider.System,
            CreateStrictMock<IHttpClientFactory>().Object,
            CreateStrictMock<IOutboundHttpContentReader>().Object,
            CreateStrictMock<IMemoryCache>().Object,
            CreateStrictMock<IClientKeyImporter>().Object,
            CreateStrictMock<ISecretSerializer>().Object,
            _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>()
        );
    }

    private SecretKey StoredPublicKey { get; }
    private SecretKey SharedSecret { get; }
    private DefaultClientKeyResolver Resolver { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _rsa.Dispose();
    }

    private OpenIdClient CreateClient(
        IReadOnlyDictionary<string, object> settings,
        bool readsClientId
    )
    {
        var storedKeys = _serviceProvider
            .GetRequiredService<ISecretKeyCollectionFactory>()
            .Create([SharedSecret, StoredPublicKey]);

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(new StubSettings(settings)).Verifiable();
        mockClient.SetupGet(x => x.SecretKeys).Returns(storedKeys).Verifiable();
        if (readsClientId)
            mockClient.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();
        return mockClient.Object;
    }

    #region ResolveSecretKeysAsync Tests

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenClientHasNoJwksUri_ReturnsItsStoredKeys()
    {
        var client = CreateClient(new Dictionary<string, object>(), readsClientId: false);

        var keys = await Resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        Assert.Same(client.SecretKeys, keys);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenJwksUriCannotBeFetched_ReturnsOnlySharedSecrets()
    {
        var client = CreateClient(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.JwksUri] = "http://client.example/jwks",
            },
            readsClientId: true
        );

        var keys = await Resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        // A stored public key never stands in for the keys the client publishes at its jwks_uri.
        var key = Assert.Single(keys);
        Assert.Same(SharedSecret, key);
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

        public ISettingCollection Combine(IEnumerable<Setting> otherCollection) =>
            throw new NotSupportedException();

        public IEnumerator<Setting> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
