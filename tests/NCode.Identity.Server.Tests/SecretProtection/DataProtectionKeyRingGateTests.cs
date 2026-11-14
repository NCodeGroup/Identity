// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DataProtectionKeyRingGateTests : SecretProtectionTestBase
{
    private static SecretOwner PlatformOwner { get; } = SecretOwner.Platform;

    private static DateTimeOffset Now { get; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static Guid LiveKeyId { get; } = Guid.Parse("6d1f7c0e-0c62-4d6e-9a71-1d6a2f3d9b11");

    private static Guid OtherKeyId { get; } = Guid.Parse("0b9c2b5e-3f4d-4f6a-8c7e-5a1d2e3f4a5b");

    #region Helpers

    private Mock<IKey> CreateKey(Guid keyId, bool isRevoked, DateTimeOffset expirationDate)
    {
        var mockKey = CreateStrictMock<IKey>();
        mockKey.Setup(x => x.KeyId).Returns(keyId).Verifiable();
        mockKey.Setup(x => x.IsRevoked).Returns(isRevoked).Verifiable();
        mockKey.Setup(x => x.ExpirationDate).Returns(expirationDate).Verifiable();
        return mockKey;
    }

    private Mock<IKey> CreateLiveKey(Guid keyId) => CreateKey(keyId, false, Now.AddDays(30));

    private Mock<IKeyManager> CreateKeyManager(params IKey[] keys)
    {
        var mockKeyManager = CreateStrictMock<IKeyManager>();
        mockKeyManager.Setup(x => x.GetAllKeys()).Returns(keys).Verifiable();
        return mockKeyManager;
    }

    private static FakeTimeProvider CreateTimeProvider() => new(Now);

    private DataProtectionKeyRingGate CreateGate(
        IKeyManager keyManager,
        TimeProvider timeProvider,
        DataProtectionKeyRingOptions? keyRingOptions = null,
        bool localDevelopmentEnabled = false
    ) =>
        new(
            keyManager,
            Options.Create(keyRingOptions ?? new DataProtectionKeyRingOptions()),
            Options.Create(new LocalDevelopmentOptions { Enabled = localDevelopmentEnabled }),
            timeProvider,
            CreateLooseMock<ILogger<DataProtectionKeyRingGate>>().Object
        );

    private async ValueTask<PlatformSecretProtectorHolder> CreateLoadedHolderAsync(
        CancellationToken cancellationToken
    ) =>
        new()
        {
            Protector = await GetProtectorAsync(
                CreateSecretServices(PlatformOwner),
                PlatformOwner,
                cancellationToken
            ),
        };

    private ServiceProvider CreateDataProtectionServices(
        IXmlRepository repository,
        PlatformSecretProtectorHolder holder
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(holder);
        serviceCollection.AddDataProtection().SetApplicationName("key-ring-gate-tests");
        serviceCollection.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = repository;
            options.XmlEncryptor = new MasterKeyXmlEncryptor(holder);
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();
        ServiceProviders.Add(serviceProvider);
        return serviceProvider;
    }

    // An in-memory key ring repository that hands out copies, as a database would.
    private IXmlRepository CreateRepository(List<XElement> elements)
    {
        var repository = CreateStrictMock<IXmlRepository>();
        repository
            .Setup(x => x.GetAllElements())
            .Returns(() => elements.Select(element => new XElement(element)).ToList())
            .Verifiable();
        repository
            .Setup(x => x.StoreElement(It.IsAny<XElement>(), It.IsAny<string>()))
            .Callback<XElement, string>((element, _) => elements.Add(new XElement(element)))
            .Verifiable();
        return repository.Object;
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenEveryLiveKeyDecrypts_Completes()
    {
        var descriptor = CreateStrictMock<IAuthenticatedEncryptorDescriptor>().Object;
        var key = CreateLiveKey(LiveKeyId);
        key.Setup(x => x.Descriptor).Returns(descriptor).Verifiable();
        var gate = CreateGate(CreateKeyManager(key.Object).Object, CreateTimeProvider());

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenALiveKeyCannotBeDecrypted_ThrowsNamingTheKey()
    {
        var key = CreateLiveKey(LiveKeyId);
        key.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var gate = CreateGate(CreateKeyManager(key.Object).Object, CreateTimeProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.StartAsync(CancellationToken.None)
        );

        Assert.Contains(LiveKeyId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.StartsWith(
            "Refusing to start a deployment",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "DataProtectionKeyRing:RevokedKeyIds",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("LocalDevelopment:Enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenSeveralLiveKeysCannotBeDecrypted_ThrowsNamingEveryKey()
    {
        var first = CreateLiveKey(LiveKeyId);
        first.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var second = CreateLiveKey(OtherKeyId);
        second.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var gate = CreateGate(
            CreateKeyManager(first.Object, second.Object).Object,
            CreateTimeProvider()
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.StartAsync(CancellationToken.None)
        );

        Assert.Contains(LiveKeyId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(OtherKeyId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("2 live key(s)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenAnUndecryptableKeyIsRevokedOrExpired_IgnoresIt()
    {
        var revokedKey = CreateKey(LiveKeyId, true, Now.AddDays(30));
        var expiredKey = CreateKey(OtherKeyId, false, Now.AddDays(-1));
        var gate = CreateGate(
            CreateKeyManager(revokedKey.Object, expiredKey.Object).Object,
            CreateTimeProvider()
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenAKeyIsListedForRevocation_RevokesItWithoutDecryptingIt()
    {
        var key = CreateLiveKey(LiveKeyId);
        var keyManager = CreateKeyManager(key.Object);
        keyManager
            .Setup(x =>
                x.RevokeKey(LiveKeyId, DataProtectionKeyRingGate.ConfiguredRevocationReason)
            )
            .Verifiable();
        var gate = CreateGate(
            keyManager.Object,
            CreateTimeProvider(),
            new DataProtectionKeyRingOptions { RevokedKeyIds = [LiveKeyId] }
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenAListedKeyIsAlreadyRevoked_DoesNotRevokeItAgain()
    {
        var key = CreateKey(LiveKeyId, true, Now.AddDays(30));
        var gate = CreateGate(
            CreateKeyManager(key.Object).Object,
            CreateTimeProvider(),
            new DataProtectionKeyRingOptions { RevokedKeyIds = [LiveKeyId] }
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenALiveKeyCannotBeDecryptedAndLocalDevelopmentIsEnabled_RevokesIt()
    {
        var key = CreateLiveKey(LiveKeyId);
        key.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var keyManager = CreateKeyManager(key.Object);
        keyManager
            .Setup(x =>
                x.RevokeKey(LiveKeyId, DataProtectionKeyRingGate.LocalDevelopmentRevocationReason)
            )
            .Verifiable();
        var gate = CreateGate(
            keyManager.Object,
            CreateTimeProvider(),
            localDevelopmentEnabled: true
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenTheRingWasEncryptedUnderAnotherMasterKey_Throws()
    {
        var holderA = await CreateLoadedHolderAsync(CancellationToken.None);
        var holderB = await CreateLoadedHolderAsync(CancellationToken.None);
        var repository = CreateRepository([]);
        var servicesA = CreateDataProtectionServices(repository, holderA);
        var servicesB = CreateDataProtectionServices(repository, holderB);
        var now = TimeProvider.System.GetUtcNow();
        var key = servicesA.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        var gate = CreateGate(servicesB.GetRequiredService<IKeyManager>(), TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.StartAsync(CancellationToken.None)
        );

        Assert.Contains(key.KeyId.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenTheRingWasEncryptedUnderThePlatformMasterKey_Completes()
    {
        var holderA = await CreateLoadedHolderAsync(CancellationToken.None);
        var repository = CreateRepository([]);
        var servicesA = CreateDataProtectionServices(repository, holderA);
        var now = TimeProvider.System.GetUtcNow();
        servicesA.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        var gate = CreateGate(servicesA.GetRequiredService<IKeyManager>(), TimeProvider.System);

        await gate.StartAsync(CancellationToken.None);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Completes()
    {
        var gate = CreateGate(
            CreateStrictMock<IKeyManager>().Object,
            CreateStrictMock<TimeProvider>().Object
        );

        await gate.StopAsync(CancellationToken.None);
    }

    #endregion
}
