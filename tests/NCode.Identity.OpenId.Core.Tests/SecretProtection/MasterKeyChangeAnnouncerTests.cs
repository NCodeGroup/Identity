// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyChangeAnnouncerTests : IDisposable
{
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("acme");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IReplicaBroadcaster> _broadcaster;
    private readonly Mock<IJobDispatcher> _dispatcher;

    public MasterKeyChangeAnnouncerTests()
    {
        _broadcaster = _mocks.Create<IReplicaBroadcaster>();
        _dispatcher = _mocks.Create<IJobDispatcher>();
    }

    public void Dispose() => _mocks.Verify();

    private MasterKeyChangeAnnouncer CreateAnnouncer() =>
        new(
            _broadcaster.Object,
            _dispatcher.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions
                {
                    MasterKeys = new MasterKeyOptions { RefreshInterval = TimeSpan.FromMinutes(5) },
                }
            )
        );

    private static MasterKeyInfo Info(int version, DateTimeOffset? retiredWhen = null) =>
        new(
            Guid.NewGuid(),
            Owner,
            version,
            DateTimeOffset.UnixEpoch,
            retiredWhen,
            "local",
            "root",
            null
        );

    [Fact]
    public async Task Rotated_BroadcastsTheOwnerAndDispatchesADelayedRewrapJob()
    {
        _broadcaster
            .Setup(x =>
                x.PublishAsync("master-key-changed", "tenant:acme", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask);
        DispatchOptions? options = null;
        MasterKeyRewrapJob? job = null;
        _dispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<MasterKeyRewrapJob>(),
                    It.IsAny<DispatchOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<MasterKeyRewrapJob, DispatchOptions?, CancellationToken>(
                (j, o, _) =>
                {
                    job = j;
                    options = o;
                }
            )
            .Returns(ValueTask.CompletedTask);

        await CreateAnnouncer()
            .RotatedAsync(
                new MasterKeyRotation(Owner, Info(2), Info(1, DateTimeOffset.UnixEpoch)),
                CancellationToken.None
            );

        Assert.Equal("tenant", job?.OwnerType);
        Assert.Equal("acme", job?.OwnerId);
        Assert.Equal(TimeSpan.FromMinutes(10), options?.Delay);
        Assert.Equal(5, options?.MaxAttempts);
    }

    [Fact]
    public async Task Rotated_WithoutARetiredKey_BroadcastsOnly()
    {
        _broadcaster
            .Setup(x =>
                x.PublishAsync("master-key-changed", "tenant:acme", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask);

        await CreateAnnouncer()
            .RotatedAsync(new MasterKeyRotation(Owner, Info(1), null), CancellationToken.None);
    }

    [Fact]
    public async Task Destroyed_BroadcastsTheOwner()
    {
        _broadcaster
            .Setup(x =>
                x.PublishAsync("master-key-changed", "tenant:acme", It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask);

        await CreateAnnouncer()
            .DestroyedAsync(Info(1, DateTimeOffset.UnixEpoch), CancellationToken.None);
    }
}
