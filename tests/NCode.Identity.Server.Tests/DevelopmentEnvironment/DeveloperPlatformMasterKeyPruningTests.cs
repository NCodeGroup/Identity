// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperPlatformMasterKeyPruningTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    #region Helpers

    private static bool IsEmpty(IReadOnlyCollection<string> protectedValues) =>
        protectedValues.Count == 0;

    private static DeveloperPlatformMasterKeyPruning CreatePruning(
        Mock<ISecretProtectorProvider> protectorProvider,
        Mock<IStoreManagerFactory> storeManagerFactory,
        Mock<IKeyManager> keyManager
    ) =>
        new(
            protectorProvider.Object,
            new DeveloperMasterKeyReset(storeManagerFactory.Object, protectorProvider.Object),
            keyManager.Object,
            new FakeTimeProvider(Now),
            NullLogger<DeveloperPlatformMasterKeyPruning>.Instance
        );

    #endregion

    #region StartingAsync Tests

    [Fact]
    public async Task StartingAsync_WhenTheMasterKeysUnwrap_RemovesAndRevokesNothing()
    {
        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.Platform,
                    It.Is<IReadOnlyCollection<string>>(v => IsEmpty(v)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateStrictMock<ISecretProtector>().Object)
            .Verifiable();

        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        var keyManager = CreateStrictMock<IKeyManager>();

        var pruning = CreatePruning(protectorProvider, storeManagerFactory, keyManager);

        await pruning.StartingAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartingAsync_WhenTheMasterKeysCannotBeUnwrapped_ReplacesThemAndRevokesTheKeyRing()
    {
        var calls = new List<string>();

        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.Platform,
                    It.Is<IReadOnlyCollection<string>>(v => IsEmpty(v)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CryptographicException("cannot unwrap"))
            .Verifiable();
        protectorProvider
            .Setup(x => x.Evict(SecretOwner.Platform))
            .Callback(() => calls.Add("evict"))
            .Verifiable();

        var masterKeyStore = CreateStrictMock<IMasterKeyStore>();
        masterKeyStore
            .Setup(x => x.RemoveAllAsync(SecretOwner.Platform, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("remove"))
            .ReturnsAsync(1)
            .Verifiable();

        var storeManager = CreateStrictMock<IStoreManager>();
        storeManager
            .Setup(x => x.GetStore<IMasterKeyStore>())
            .Returns(masterKeyStore.Object)
            .Verifiable();
        storeManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();

        var keyManager = CreateStrictMock<IKeyManager>();
        keyManager
            .Setup(x => x.RevokeAllKeys(Now, DeveloperPlatformMasterKeyPruning.RevocationReason))
            .Callback(() => calls.Add("revoke"))
            .Verifiable();

        var pruning = CreatePruning(protectorProvider, storeManagerFactory, keyManager);

        await pruning.StartingAsync(CancellationToken.None);

        Assert.Equal(["remove", "save", "evict", "revoke"], calls);
    }

    [Fact]
    public async Task StartingAsync_WhenTheProbeFailsOtherwise_Propagates()
    {
        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.Platform,
                    It.Is<IReadOnlyCollection<string>>(v => IsEmpty(v)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("not a key problem"))
            .Verifiable();

        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        var keyManager = CreateStrictMock<IKeyManager>();

        var pruning = CreatePruning(protectorProvider, storeManagerFactory, keyManager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pruning.StartingAsync(CancellationToken.None)
        );
    }

    #endregion

    #region Other Lifecycle Tests

    [Fact]
    public void LifecycleMethods_OtherThanStarting_CompleteAtOnce()
    {
        var pruning = CreatePruning(
            CreateStrictMock<ISecretProtectorProvider>(),
            CreateStrictMock<IStoreManagerFactory>(),
            CreateStrictMock<IKeyManager>()
        );

        Assert.True(pruning.StartAsync(CancellationToken.None).IsCompletedSuccessfully);
        Assert.True(pruning.StartedAsync(CancellationToken.None).IsCompletedSuccessfully);
        Assert.True(pruning.StoppingAsync(CancellationToken.None).IsCompletedSuccessfully);
        Assert.True(pruning.StopAsync(CancellationToken.None).IsCompletedSuccessfully);
        Assert.True(pruning.StoppedAsync(CancellationToken.None).IsCompletedSuccessfully);
    }

    #endregion
}
