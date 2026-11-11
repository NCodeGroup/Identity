// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.DevelopmentEnvironment;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperMasterKeyResetTests : BaseTests
{
    #region ResetAsync Tests

    [Fact]
    public async Task ResetAsync_RemovesTheMasterKeysInACommittedUnitOfWorkThenEvictsTheOwner()
    {
        var owner = SecretOwner.ForTenant("tenant-1");
        var calls = new List<string>();

        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x => x.Evict(owner))
            .Callback(() => calls.Add("evict"))
            .Verifiable();

        var masterKeyStore = CreateStrictMock<IMasterKeyStore>();
        masterKeyStore
            .Setup(x => x.RemoveAllAsync(owner, It.IsAny<CancellationToken>()))
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

        var reset = new DeveloperMasterKeyReset(
            storeManagerFactory.Object,
            protectorProvider.Object
        );

        await reset.ResetAsync(owner, CancellationToken.None);

        // The deletion is committed before the key ring forgets the master keys, so its next load sees the deletion.
        Assert.Equal(["remove", "save", "evict"], calls);
    }

    #endregion
}
