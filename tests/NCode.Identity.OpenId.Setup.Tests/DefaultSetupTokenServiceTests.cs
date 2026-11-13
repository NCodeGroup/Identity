// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Setup.Logging;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

public sealed class DefaultSetupTokenServiceTests : IDisposable
{
    private const string ConfiguredToken = "a-configured-setup-token-of-32-or-more-characters";
    private const string StoredToken = "a-stored-setup-token";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IInstanceSetupStore> MockInstanceSetupStore { get; }
    private Mock<ILogger<DefaultSetupTokenService>> MockLogger { get; }
    private FakeTimeProvider TimeProvider { get; }
    private SetupProcessClock ProcessClock { get; }

    public DefaultSetupTokenServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockInstanceSetupStore = MockRepository.Create<IInstanceSetupStore>();
        MockLogger = MockRepository.Create<ILogger<DefaultSetupTokenService>>();

        TimeProvider = new FakeTimeProvider();
        ProcessClock = new SetupProcessClock(TimeProvider);
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private DefaultSetupTokenService CreateService(string? token = null) =>
        new(
            MockStoreManagerFactory.Object,
            Options.Create(new SetupOptions { Token = token }),
            TimeProvider,
            ProcessClock,
            MockLogger.Object
        );

    private static byte[] Hash(string token) =>
        SHA256.HashData(SecureEncoding.UTF8.GetBytes(token));

    private static PersistedInstanceSetup CreateRow(
        string? token = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null,
        bool isInitialized = false
    ) =>
        new()
        {
            Key = PersistedInstanceSetup.SingletonKey,
            IsInitialized = isInitialized,
            TokenHash = token is null ? null : Hash(token),
            TokenIssuedAt = issuedAt,
            TokenExpiresAt = expiresAt,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
        };

    private void SetupStoreManagers(Times times)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable(times);
        MockStoreManager
            .Setup(x => x.GetStore<IInstanceSetupStore>())
            .Returns(MockInstanceSetupStore.Object)
            .Verifiable(times);
        MockStoreManager
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask)
            .Verifiable(times);
    }

    private void SetupRead(PersistedInstanceSetup? row, int storeManagers = 1)
    {
        SetupStoreManagers(Times.Exactly(storeManagers));
        MockInstanceSetupStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(row)
            .Verifiable(Times.Once());
    }

    private void SetupUpdate(PersistedInstanceSetup row)
    {
        MockInstanceSetupStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedInstanceSetup>(item => ReferenceEquals(item, row)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    private void SetupLogged(LogLevel level, int eventId, Dictionary<string, object?> state)
    {
        MockLogger.Setup(x => x.IsEnabled(level)).Returns(true).Verifiable(Times.Once());
#pragma warning disable CA1873 // The arguments are matchers of a mock setup, not log arguments.
        MockLogger
            .Setup(x =>
                x.Log(
                    level,
                    It.Is<EventId>(id => id.Id == eventId),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
#pragma warning restore CA1873
            .Callback(
                new InvocationAction(invocation =>
                {
                    var pairs = Assert.IsAssignableFrom<
                        IReadOnlyList<KeyValuePair<string, object?>>
                    >(invocation.Arguments[2]);
                    foreach (var (key, value) in pairs)
                    {
                        state[key] = value;
                    }
                })
            )
            .Verifiable(Times.Once());
    }

    #endregion

    #region TokenSource Tests

    [Fact]
    public void TokenSource_WhenTokenIsConfigured_IsConfigured()
    {
        var service = CreateService(ConfiguredToken);

        Assert.Equal(SetupTokenSources.Configured, service.TokenSource);
    }

    [Fact]
    public void TokenSource_WhenTokenIsNotConfigured_IsGenerated()
    {
        var service = CreateService();

        Assert.Equal(SetupTokenSources.Generated, service.TokenSource);
    }

    #endregion

    #region VerifyAsync Tests

    [Fact]
    public async Task VerifyAsync_WhenConfiguredTokenMatches_ReturnsTrue()
    {
        var service = CreateService(ConfiguredToken);

        var result = await service.VerifyAsync(ConfiguredToken, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenConfiguredTokenDiffers_ReturnsFalse()
    {
        var service = CreateService(ConfiguredToken);

        var result = await service.VerifyAsync(ConfiguredToken + "x", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenGeneratedTokenMatchesAndIsUnexpired_ReturnsTrue()
    {
        var now = TimeProvider.GetUtcNow();
        SetupRead(CreateRow(StoredToken, now, now.AddMinutes(30)));
        var service = CreateService();

        var result = await service.VerifyAsync(StoredToken, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenGeneratedTokenIsWrong_ReturnsFalse()
    {
        var now = TimeProvider.GetUtcNow();
        SetupRead(CreateRow(StoredToken, now, now.AddMinutes(30)));
        var service = CreateService();

        var result = await service.VerifyAsync("a-wrong-setup-token", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenGeneratedTokenIsExpired_ReturnsFalse()
    {
        var now = TimeProvider.GetUtcNow();
        SetupRead(CreateRow(StoredToken, now, now.AddMinutes(30)));
        var service = CreateService();
        TimeProvider.Advance(TimeSpan.FromMinutes(30));

        var result = await service.VerifyAsync(StoredToken, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenInstanceIsInitialized_ReturnsFalse()
    {
        var now = TimeProvider.GetUtcNow();
        SetupRead(CreateRow(StoredToken, now, now.AddMinutes(30), isInitialized: true));
        var service = CreateService();

        var result = await service.VerifyAsync(StoredToken, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenNoTokenIsStored_ReturnsFalse()
    {
        SetupRead(CreateRow());
        var service = CreateService();

        var result = await service.VerifyAsync(StoredToken, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_WhenRowIsAbsent_ReturnsFalse()
    {
        SetupRead(null);
        var service = CreateService();

        var result = await service.VerifyAsync(StoredToken, CancellationToken.None);

        Assert.False(result);
    }

    #endregion

    #region EnsureTokenAsync Tests

    [Fact]
    public async Task EnsureTokenAsync_WhenTokenIsConfigured_DoesNothing()
    {
        var service = CreateService(ConfiguredToken);

        await service.EnsureTokenAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenNoTokenIsStored_StoresTheHashAndLogsTheTokenOnce()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var now = TimeProvider.GetUtcNow();
        var row = CreateRow();
        SetupRead(row, storeManagers: 2);
        SetupUpdate(row);
        var logged = new Dictionary<string, object?>();
        SetupLogged(LogLevel.Warning, EventIds.SetupTokenIssued, logged);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        var token = Assert.IsType<string>(logged["SetupToken"]);
        Assert.Equal(
            DefaultSetupTokenService.GeneratedTokenByteLength,
            Base64Url.DecodeFromChars(token).Length
        );
        Assert.Equal(SHA256.HashSizeInBytes, row.TokenHash?.Length);
        Assert.Equal(Hash(token), row.TokenHash);
        Assert.Equal(now, row.TokenIssuedAt);
        Assert.Equal(now.AddHours(1), row.TokenExpiresAt);
        Assert.Equal(now.AddHours(1), logged["ExpiresAt"]);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenTokenWasIssuedAfterStart_KeepsIt()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var issuedAt = ProcessClock.StartedAt.AddMinutes(1);
        var row = CreateRow(StoredToken, issuedAt, issuedAt.AddHours(1));
        SetupRead(row);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        Assert.Equal(Hash(StoredToken), row.TokenHash);
        Assert.Equal(issuedAt, row.TokenIssuedAt);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenTokenWasIssuedBeforeStart_ReplacesIt()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var now = TimeProvider.GetUtcNow();
        var issuedAt = ProcessClock.StartedAt.AddMinutes(-30);
        var row = CreateRow(StoredToken, issuedAt, issuedAt.AddHours(1));
        SetupRead(row, storeManagers: 2);
        SetupUpdate(row);
        var logged = new Dictionary<string, object?>();
        SetupLogged(LogLevel.Warning, EventIds.SetupTokenIssued, logged);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        var token = Assert.IsType<string>(logged["SetupToken"]);
        Assert.NotEqual(StoredToken, token);
        Assert.Equal(Hash(token), row.TokenHash);
        Assert.Equal(now, row.TokenIssuedAt);
        Assert.Equal(now.AddHours(1), row.TokenExpiresAt);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenInstanceIsInitialized_DoesNothing()
    {
        var issuedAt = ProcessClock.StartedAt.AddMinutes(-30);
        var row = CreateRow(StoredToken, issuedAt, issuedAt.AddHours(1), isInitialized: true);
        SetupRead(row);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        Assert.Equal(issuedAt, row.TokenIssuedAt);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenAnotherReplicaStoredATokenFirst_LogsAtInformation()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var stale = CreateRow();
        var otherIssuedAt = ProcessClock.StartedAt.AddMinutes(2);
        var other = CreateRow(StoredToken, otherIssuedAt, otherIssuedAt.AddHours(1));
        SetupStoreManagers(Times.Exactly(3));
        var reads = 0;
        MockInstanceSetupStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++reads == 1 ? stale : other)
            .Verifiable(Times.Exactly(2));
        MockInstanceSetupStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedInstanceSetup>(item => ReferenceEquals(item, stale)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => ValueTask.FromException(new StoreConcurrencyException()))
            .Verifiable(Times.Once());
        var logged = new Dictionary<string, object?>();
        SetupLogged(LogLevel.Information, EventIds.SetupTokenIssuedByAnotherReplica, logged);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        Assert.Equal(otherIssuedAt, logged["IssuedAt"]);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenRowIsAbsent_AddsAnUninitializedRowThenIssuesTheToken()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        PersistedInstanceSetup? added = null;
        SetupStoreManagers(Times.Exactly(2));
        MockInstanceSetupStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedInstanceSetup?)null)
            .Verifiable(Times.Once());
        MockInstanceSetupStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedInstanceSetup>(), It.IsAny<CancellationToken>())
            )
            .Callback<PersistedInstanceSetup, CancellationToken>((item, _) => added = item)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        MockInstanceSetupStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedInstanceSetup>(item => ReferenceEquals(item, added)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        var logged = new Dictionary<string, object?>();
        SetupLogged(LogLevel.Warning, EventIds.SetupTokenIssued, logged);
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        var row = Assert.IsType<PersistedInstanceSetup>(added);
        var token = Assert.IsType<string>(logged["SetupToken"]);
        Assert.Equal(PersistedInstanceSetup.SingletonKey, row.Key);
        Assert.False(row.IsInitialized);
        Assert.Equal(Hash(token), row.TokenHash);
    }

    [Fact]
    public async Task EnsureTokenAsync_WhenAddLosesTheRace_UsesTheRowTheOtherReplicaWrote()
    {
        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var otherIssuedAt = ProcessClock.StartedAt.AddMinutes(2);
        var other = CreateRow(StoredToken, otherIssuedAt, otherIssuedAt.AddHours(1));
        SetupStoreManagers(Times.Exactly(2));
        var reads = 0;
        MockInstanceSetupStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++reads == 1 ? null : other)
            .Verifiable(Times.Exactly(2));
        MockInstanceSetupStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedInstanceSetup>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("duplicate key")))
            .Verifiable(Times.Once());
        var service = CreateService();

        await service.EnsureTokenAsync(CancellationToken.None);

        Assert.Equal(Hash(StoredToken), other.TokenHash);
    }

    #endregion
}
