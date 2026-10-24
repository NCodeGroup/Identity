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
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DataProtectionKeyRingGateTests : SecretProtectionTestBase
{
    private static SecretOwner ServerOwner { get; } = SecretOwner.ForServer("server-a");

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

    private IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return mockEnvironment.Object;
    }

    private TimeProvider CreateTimeProvider()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        return mockTimeProvider.Object;
    }

    private DataProtectionKeyRingGate CreateGate(
        IKeyManager keyManager,
        IHostEnvironment hostEnvironment,
        TimeProvider timeProvider,
        DataProtectionKeyRingOptions? keyRingOptions = null
    ) =>
        new(
            keyManager,
            Options.Create(keyRingOptions ?? new DataProtectionKeyRingOptions()),
            hostEnvironment,
            timeProvider,
            CreateLooseMock<ILogger<DataProtectionKeyRingGate>>().Object
        );

    private async Task<ServerSecretProtectorHolder> CreateLoadedHolderAsync() =>
        new()
        {
            Protector = await GetProtectorAsync(CreateSecretServices(ServerOwner), ServerOwner),
        };

    private ServiceProvider CreateDataProtectionServices(
        IXmlRepository repository,
        ServerSecretProtectorHolder holder
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
        var gate = CreateGate(
            CreateKeyManager(key.Object).Object,
            CreateStrictMock<IHostEnvironment>().Object,
            CreateTimeProvider()
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenALiveKeyCannotBeDecrypted_ThrowsNamingTheKey()
    {
        var key = CreateLiveKey(LiveKeyId);
        key.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var gate = CreateGate(
            CreateKeyManager(key.Object).Object,
            CreateEnvironment(Environments.Production),
            CreateTimeProvider()
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.StartAsync(CancellationToken.None)
        );

        Assert.Contains(LiveKeyId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "DataProtectionKeyRing:RevokedKeyIds",
            exception.Message,
            StringComparison.Ordinal
        );
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
            CreateEnvironment(Environments.Production),
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
            CreateStrictMock<IHostEnvironment>().Object,
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
            CreateStrictMock<IHostEnvironment>().Object,
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
            CreateStrictMock<IHostEnvironment>().Object,
            CreateTimeProvider(),
            new DataProtectionKeyRingOptions { RevokedKeyIds = [LiveKeyId] }
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenALiveKeyCannotBeDecryptedInDevelopment_RevokesIt()
    {
        var key = CreateLiveKey(LiveKeyId);
        key.Setup(x => x.Descriptor).Throws<CryptographicException>().Verifiable();
        var keyManager = CreateKeyManager(key.Object);
        keyManager
            .Setup(x =>
                x.RevokeKey(LiveKeyId, DataProtectionKeyRingGate.DevelopmentRevocationReason)
            )
            .Verifiable();
        var gate = CreateGate(
            keyManager.Object,
            CreateEnvironment(Environments.Development),
            CreateTimeProvider()
        );

        await gate.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenTheRingWasEncryptedUnderAnotherMasterKey_Throws()
    {
        var holderA = await CreateLoadedHolderAsync();
        var holderB = await CreateLoadedHolderAsync();
        var repository = CreateRepository([]);
        var servicesA = CreateDataProtectionServices(repository, holderA);
        var servicesB = CreateDataProtectionServices(repository, holderB);
        var now = TimeProvider.System.GetUtcNow();
        var key = servicesA.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        var gate = CreateGate(
            servicesB.GetRequiredService<IKeyManager>(),
            CreateEnvironment(Environments.Production),
            TimeProvider.System
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.StartAsync(CancellationToken.None)
        );

        Assert.Contains(key.KeyId.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_WhenTheRingWasEncryptedUnderTheServerMasterKey_Completes()
    {
        var holderA = await CreateLoadedHolderAsync();
        var repository = CreateRepository([]);
        var servicesA = CreateDataProtectionServices(repository, holderA);
        var now = TimeProvider.System.GetUtcNow();
        servicesA.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        var gate = CreateGate(
            servicesA.GetRequiredService<IKeyManager>(),
            CreateStrictMock<IHostEnvironment>().Object,
            TimeProvider.System
        );

        await gate.StartAsync(CancellationToken.None);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Completes()
    {
        var gate = CreateGate(
            CreateStrictMock<IKeyManager>().Object,
            CreateStrictMock<IHostEnvironment>().Object,
            CreateStrictMock<TimeProvider>().Object
        );

        await gate.StopAsync(CancellationToken.None);
    }

    #endregion
}
