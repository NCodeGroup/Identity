// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
            Microsoft.Extensions.Options.Options.Create(new OutboundHttpOptions()),
            CreateStrictMock<IMemoryCache>().Object,
            CreateStrictMock<IClientKeyImporter>().Object,
            CreateStrictMock<ISecretSerializer>().Object,
            _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>()
        );
    }

    private SecretKey StoredPublicKey { get; }
    private SecretKey SharedSecret { get; }
    private DefaultClientKeyResolver Resolver { get; }
    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        MemoryCache.Dispose();
        _rsa.Dispose();
    }

    private DefaultClientKeyResolver CreateFetchingResolver(
        HttpMessageHandler handler,
        OutboundHttpOptions options
    )
    {
        var mockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(() => new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        return new DefaultClientKeyResolver(
            _serviceProvider.GetRequiredService<ILogger<DefaultClientKeyResolver>>(),
            TimeProvider.System,
            mockHttpClientFactory.Object,
            CreateStrictMock<IOutboundHttpContentReader>().Object,
            Microsoft.Extensions.Options.Options.Create(options),
            MemoryCache,
            CreateStrictMock<IClientKeyImporter>().Object,
            CreateStrictMock<ISecretSerializer>().Object,
            _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>()
        );
    }

    private DefaultClientKeyResolver CreateResolverOverCache(IMemoryCache memoryCache)
    {
        // A strict factory with no setup: any fetch of the jwks_uri fails the test.
        var mockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        var mockContentReader = CreateStrictMock<IOutboundHttpContentReader>();
        var mockClientKeyImporter = CreateStrictMock<IClientKeyImporter>();
        var mockSecretSerializer = CreateStrictMock<ISecretSerializer>();
        var options = Microsoft.Extensions.Options.Options.Create(new OutboundHttpOptions());
        var logger = _serviceProvider.GetRequiredService<ILogger<DefaultClientKeyResolver>>();
        var collectionFactory = _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>();

        return new DefaultClientKeyResolver(
            logger,
            TimeProvider.System,
            mockHttpClientFactory.Object,
            mockContentReader.Object,
            options,
            memoryCache,
            mockClientKeyImporter.Object,
            mockSecretSerializer.Object,
            collectionFactory
        );
    }

    private static bool IsUnavailableKey(object key) =>
        key is string text && text.EndsWith(":unavailable", StringComparison.Ordinal);

    private static Dictionary<string, object> FetchSettings(TimeSpan fetchTimeout) =>
        new()
        {
            [OpenIdSettingNames.JwksUri] = "https://client.example/jwks",
            [OpenIdSettingNames.JwksUriFetchTimeout] = fetchTimeout,
            [OpenIdSettingNames.JwksUriMaxSizeBytes] = 102400,
            [OpenIdSettingNames.JwksUriCacheDuration] = TimeSpan.FromMinutes(15),
        };

    private void AssertOnlySharedSecret(ISecretKeyCollection keys)
    {
        var key = Assert.Single(keys);
        Assert.Same(SharedSecret, key);
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

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenTwoRequestsMissAtOnce_FetchesOnce()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CountingHttpMessageHandler(gate.Task);
        var resolver = CreateFetchingResolver(handler, new OutboundHttpOptions());
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: true);

        var first = resolver.ResolveSecretKeysAsync(client, CancellationToken.None).AsTask();
        var second = resolver.ResolveSecretKeysAsync(client, CancellationToken.None).AsTask();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, AssertOnlySharedSecret);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenTheFetchFailed_DoesNotFetchAgain()
    {
        using var handler = new CountingHttpMessageHandler(Task.CompletedTask);
        var resolver = CreateFetchingResolver(handler, new OutboundHttpOptions());
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: true);

        var first = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);
        var second = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        AssertOnlySharedSecret(first);
        AssertOnlySharedSecret(second);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenTheFailureIsNoLongerRemembered_FetchesAgain()
    {
        using var handler = new CountingHttpMessageHandler(Task.CompletedTask);
        var options = new OutboundHttpOptions
        {
            FailureCacheDuration = TimeSpan.FromMilliseconds(1),
        };
        var resolver = CreateFetchingResolver(handler, options);
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: true);

        var first = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        var second = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        AssertOnlySharedSecret(first);
        AssertOnlySharedSecret(second);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenAnotherClientHasOtherLimits_FetchesSeparately()
    {
        using var handler = new CountingHttpMessageHandler(Task.CompletedTask);
        var resolver = CreateFetchingResolver(handler, new OutboundHttpOptions());
        var firstSettings = FetchSettings(TimeSpan.FromSeconds(10));
        var firstClient = CreateClient(firstSettings, readsClientId: true);
        var secondSettings = FetchSettings(TimeSpan.FromSeconds(5));
        var secondClient = CreateClient(secondSettings, readsClientId: true);

        var first = await resolver.ResolveSecretKeysAsync(firstClient, CancellationToken.None);
        var second = await resolver.ResolveSecretKeysAsync(secondClient, CancellationToken.None);

        AssertOnlySharedSecret(first);
        AssertOnlySharedSecret(second);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenTheFirstCallerGivesUp_CompletesTheFetchForTheOthers()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CountingHttpMessageHandler(gate.Task);
        var resolver = CreateFetchingResolver(handler, new OutboundHttpOptions());
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: true);
        using var cancellation = new CancellationTokenSource();

        var first = resolver.ResolveSecretKeysAsync(client, cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        var second = resolver.ResolveSecretKeysAsync(client, CancellationToken.None).AsTask();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        var keys = await second;
        AssertOnlySharedSecret(keys);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenAFailureIsRememberedBeforeTheFetchStarts_DoesNotFetch()
    {
        var unavailableChecks = 0;
        object? notCached = null;
        object? remembered = true;
        var mockMemoryCache = CreateStrictMock<IMemoryCache>();
        mockMemoryCache
            .Setup(x => x.TryGetValue(It.Is<object>(key => !IsUnavailableKey(key)), out notCached))
            .Returns(false)
            .Verifiable();
        // The caller's check misses; a fetch that ends before this one starts has remembered its failure by then.
        mockMemoryCache
            .Setup(x => x.TryGetValue(It.Is<object>(key => IsUnavailableKey(key)), out remembered))
            .Returns(() => Interlocked.Increment(ref unavailableChecks) > 1)
            .Verifiable();
        var resolver = CreateResolverOverCache(mockMemoryCache.Object);
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: true);

        var keys = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        AssertOnlySharedSecret(keys);
        Assert.Equal(2, unavailableChecks);
    }

    [Fact]
    public async Task ResolveSecretKeysAsync_WhenAKeySetIsCachedBeforeTheFetchStarts_ReturnsItWithoutFetching()
    {
        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        var publishedMetadata = new KeyMetadata
        {
            KeyId = "published-key",
            Use = SecretKeyUses.Signature,
        };
        var publishedKey = secretKeyFactory.CreateRsaPublicKey(publishedMetadata, _rsa);
        var acceptedChecks = 0;
        object? accepted = new List<SecretKey> { publishedKey };
        object? notRemembered = null;
        var mockMemoryCache = CreateStrictMock<IMemoryCache>();
        // The caller's check misses; a fetch that ends before this one starts has cached its key set by then.
        mockMemoryCache
            .Setup(x => x.TryGetValue(It.Is<object>(key => !IsUnavailableKey(key)), out accepted))
            .Returns(() => Interlocked.Increment(ref acceptedChecks) > 1)
            .Verifiable();
        mockMemoryCache
            .Setup(x =>
                x.TryGetValue(It.Is<object>(key => IsUnavailableKey(key)), out notRemembered)
            )
            .Returns(false)
            .Verifiable();
        var resolver = CreateResolverOverCache(mockMemoryCache.Object);
        var settings = FetchSettings(TimeSpan.FromSeconds(10));
        var client = CreateClient(settings, readsClientId: false);

        var keys = await resolver.ResolveSecretKeysAsync(client, CancellationToken.None);

        SecretKey[] expected = [SharedSecret, publishedKey];
        Assert.Equal(expected, keys);
        Assert.Equal(2, acceptedChecks);
    }

    #endregion

    #region Scaffolding Types

    private sealed class CountingHttpMessageHandler(Task gate) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _calls);
            await gate.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
    }

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
