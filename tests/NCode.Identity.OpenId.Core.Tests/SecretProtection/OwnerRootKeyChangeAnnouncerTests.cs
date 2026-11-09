// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class OwnerRootKeyChangeAnnouncerTests : BaseTests
{
    private Mock<IReplicaBroadcaster> Broadcaster { get; }
    private Mock<IMediator> Mediator { get; }
    private Mock<IStoreManagerFactory> StoreManagerFactory { get; }
    private Mock<IStoreManager> StoreManager { get; }
    private Mock<ITenantStore> TenantStore { get; }
    private ServiceProvider Services { get; }

    public OwnerRootKeyChangeAnnouncerTests()
    {
        Broadcaster = CreateStrictMock<IReplicaBroadcaster>();
        Mediator = CreateStrictMock<IMediator>();
        StoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        StoreManager = CreateStrictMock<IStoreManager>();
        TenantStore = CreateStrictMock<ITenantStore>();

        var services = new ServiceCollection();
        services.AddScoped(_ => Mediator.Object);
        Services = services.BuildServiceProvider();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Services.Dispose();

        base.Dispose(disposing);
    }

    private OwnerRootKeyChangeAnnouncer CreateAnnouncer() =>
        new(
            Services.GetRequiredService<IServiceScopeFactory>(),
            Broadcaster.Object,
            StoreManagerFactory.Object
        );

    private void SetupStoreManager()
    {
        StoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoreManager.Object)
            .Verifiable();
        StoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(TenantStore.Object)
            .Verifiable();
        StoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupSend(CancellationToken cancellationToken, Action? onSend = null)
    {
        Mediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<TenantChangedCommand>(command =>
                        command.TenantId == "acme" && !command.SettingsChanged
                    ),
                    cancellationToken
                )
            )
            .Callback(() => onSend?.Invoke())
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    [Fact]
    public async Task Changed_ForATenant_MarksTheSecretsChangedBeforeItSendsTheCommand()
    {
        using var source = new CancellationTokenSource();
        var calls = new List<string>();
        SetupStoreManager();
        TenantStore
            .Setup(x => x.MarkSecretsChangedAsync("acme", source.Token))
            .Callback(() => calls.Add("mark"))
            .ReturnsAsync(true)
            .Verifiable();
        StoreManager
            .Setup(x => x.SaveChangesAsync(source.Token))
            .Callback(() => calls.Add("save"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSend(source.Token, () => calls.Add("send"));

        await CreateAnnouncer().ChangedAsync(SecretOwner.ForTenant("acme"), source.Token);

        Assert.Equal(["mark", "save", "send"], calls);
    }

    [Fact]
    public async Task Changed_ForATenantThatDoesNotExist_SendsTheCommandWithoutSaving()
    {
        using var source = new CancellationTokenSource();
        SetupStoreManager();
        TenantStore
            .Setup(x => x.MarkSecretsChangedAsync("acme", source.Token))
            .ReturnsAsync(false)
            .Verifiable();
        SetupSend(source.Token);

        await CreateAnnouncer().ChangedAsync(SecretOwner.ForTenant("acme"), source.Token);
    }

    [Fact]
    public async Task Changed_WhenMarkingTheSecretsFails_SendsTheCommandAndThrows()
    {
        using var source = new CancellationTokenSource();
        SetupStoreManager();
        TenantStore
            .Setup(x => x.MarkSecretsChangedAsync("acme", source.Token))
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();
        SetupSend(source.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateAnnouncer().ChangedAsync(SecretOwner.ForTenant("acme"), source.Token)
        );
    }

    [Fact]
    public async Task Changed_ForAnotherOwnerType_BroadcastsTheOwnerOnTheMasterKeyChannel()
    {
        Broadcaster
            .Setup(x =>
                x.PublishAsync("master-key-changed", "partner:acme", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateAnnouncer()
            .ChangedAsync(new SecretOwner("partner", "acme"), CancellationToken.None);

        StoreManagerFactory.VerifyNoOtherCalls();
    }
}
