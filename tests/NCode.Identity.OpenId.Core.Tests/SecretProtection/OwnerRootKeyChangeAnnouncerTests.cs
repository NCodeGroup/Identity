// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class OwnerRootKeyChangeAnnouncerTests : BaseTests
{
    private readonly Mock<IReplicaBroadcaster> _broadcaster;
    private readonly Mock<IMediator> _mediator;
    private readonly ServiceProvider _services;

    public OwnerRootKeyChangeAnnouncerTests()
    {
        _broadcaster = CreateStrictMock<IReplicaBroadcaster>();
        _mediator = CreateStrictMock<IMediator>();

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
        new(_services.GetRequiredService<IServiceScopeFactory>(), _broadcaster.Object);

    [Fact]
    public async Task Changed_ForATenant_SendsTheTenantChangedCommandWithoutASettingsChange()
    {
        using var source = new CancellationTokenSource();
        _mediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<TenantChangedCommand>(command =>
                        command.TenantId == "acme" && !command.SettingsChanged
                    ),
                    source.Token
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateAnnouncer().ChangedAsync(SecretOwner.ForTenant("acme"), source.Token);
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
    }
}
