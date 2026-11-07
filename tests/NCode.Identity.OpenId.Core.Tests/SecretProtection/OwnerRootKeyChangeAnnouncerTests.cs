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
    private readonly Mock<IReplicaBroadcaster> _broadcaster;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IStoreManager> _storeManager;
    private readonly Mock<ITenantStore> _tenantStore;
    private readonly ServiceProvider _services;

    public OwnerRootKeyChangeAnnouncerTests()
    {
        _broadcaster = CreateStrictMock<IReplicaBroadcaster>();
        _mediator = CreateStrictMock<IMediator>();
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        _storeManager = CreateStrictMock<IStoreManager>();
        _tenantStore = CreateStrictMock<ITenantStore>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _mediator.Object);
        _services = services.BuildServiceProvider();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _services.Dispose();

        base.Dispose(disposing);
    }

    private OwnerRootKeyChangeAnnouncer CreateAnnouncer() =>
        new(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _broadcaster.Object,
            _storeManagerFactory.Object
        );

    private void SetupStoreManager()
    {
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_storeManager.Object)
            .Verifiable();
        _storeManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(_tenantStore.Object)
            .Verifiable();
        _storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupSend(CancellationToken cancellationToken, Action? onSend = null)
    {
        _mediator
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
        _tenantStore
            .Setup(x => x.MarkSecretsChangedAsync("acme", source.Token))
            .Callback(() => calls.Add("mark"))
            .ReturnsAsync(true)
            .Verifiable();
        _storeManager
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
        _tenantStore
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
        _tenantStore
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
        _broadcaster
            .Setup(x =>
                x.PublishAsync("master-key-changed", "partner:acme", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateAnnouncer()
            .ChangedAsync(new SecretOwner("partner", "acme"), CancellationToken.None);

        _storeManagerFactory.VerifyNoOtherCalls();
    }
}
