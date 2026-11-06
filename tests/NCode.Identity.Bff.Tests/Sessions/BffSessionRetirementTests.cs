// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffSessionRetirementTests : BaseTests
{
    private const string RemovedFrontend = "removed";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static readonly BffSessionClient CurrentClient = new(
        BffTestFrontends.Name,
        BffTestFrontends.Authority,
        BffTestFrontends.ClientId
    );

    private readonly Mock<IBffSessionStore> _store;
    private readonly Mock<IBffSessionManager> _manager;
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public BffSessionRetirementTests()
    {
        _store = CreateStrictMock<IBffSessionStore>();
        _manager = CreateStrictMock<IBffSessionManager>();
    }

    private BffSessionRetirement CreateRetirement(ILogger<BffSessionRetirement>? logger = null) =>
        new(
            BffTestTelemetry.Instrument(_store.Object),
            _manager.Object,
            _timeProvider,
            logger ?? NullLogger<BffSessionRetirement>.Instance
        );

    private static BffFrontendSet CreateSet(Action<BffFrontendOptions>? configure = null) =>
        BffFrontendSet.Create(
            new BffOptions
            {
                Frontends = { [BffTestFrontends.Name] = BffTestFrontends.CreateOptions(configure) },
            },
            []
        );

    private static BffSessionMetadata CreateMetadata(BffSessionClient client) =>
        new()
        {
            Frontend = client.Frontend,
            Authority = client.Authority,
            ClientId = client.ClientId,
            Issuer = client.Authority,
            Subject = "sub-1",
            CreatedAt = Now,
            LastAccessedAt = Now,
            AbsoluteExpiresAt = Now.AddHours(8),
            IdleExpiresAt = Now.AddHours(1),
        };

    private static BffSessionRecord CreateRecord(string key, BffSessionMetadata metadata) =>
        new(key, 1, metadata, new byte[] { 1 });

    private void SetupFindClients(params BffSessionClient[] clients) =>
        _store
            .Setup(x => x.FindClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clients)
            .Verifiable();

    private void SetupFindByClient(BffSessionClient client, params string[] keys) =>
        _store
            .Setup(x => x.FindByClientAsync(client, It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys)
            .Verifiable();

    private void SetupGet(string key, BffSessionRecord? record) =>
        _store
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record)
            .Verifiable();

    private void SetupAcquire(string frontend, BffRefreshLease? lease) =>
        _store
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(
                    "retire:" + frontend,
                    Now + BffSessionRetirement.LeaseDuration,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(lease)
            .Verifiable();

    private void SetupRelease(BffRefreshLease lease) =>
        _store
            .Setup(x => x.ReleaseRefreshLeaseAsync(lease, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupEnd(string key, BffSessionMetadata metadata, string reason) =>
        _manager
            .Setup(x => x.EndAsync(null, key, metadata, reason, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region RetireAsync Tests

    [Fact]
    public async Task RetireAsync_CurrentClient_EndsNothing()
    {
        SetupFindClients(CurrentClient);
        var retirement = CreateRetirement();

        Assert.True(await retirement.RetireAsync(CreateSet(), CancellationToken.None));
    }

    [Fact]
    public async Task RetireAsync_RemovedFrontend_EndsItsSessionsAsFrontendRemoved()
    {
        var client = CurrentClient with { Frontend = RemovedFrontend };
        var first = CreateMetadata(client);
        var second = CreateMetadata(client);
        var lease = new BffRefreshLease("retire:" + RemovedFrontend, "owner");
        SetupFindClients(client);
        SetupAcquire(RemovedFrontend, lease);
        SetupFindByClient(client, "key-1", "key-2");
        SetupGet("key-1", CreateRecord("key-1", first));
        SetupGet("key-2", CreateRecord("key-2", second));
        SetupEnd("key-1", first, BffSessionEndReasons.FrontendRemoved);
        SetupEnd("key-2", second, BffSessionEndReasons.FrontendRemoved);
        SetupRelease(lease);
        var retirement = CreateRetirement();

        Assert.True(await retirement.RetireAsync(CreateSet(), CancellationToken.None));
    }

    [Fact]
    public async Task RetireAsync_ChangedClientId_EndsItsSessionsAsClientChanged()
    {
        var metadata = CreateMetadata(CurrentClient);
        var lease = new BffRefreshLease("retire:" + BffTestFrontends.Name, "owner");
        SetupFindClients(CurrentClient);
        SetupAcquire(BffTestFrontends.Name, lease);
        SetupFindByClient(CurrentClient, "key-1");
        SetupGet("key-1", CreateRecord("key-1", metadata));
        SetupEnd("key-1", metadata, BffSessionEndReasons.ClientChanged);
        SetupRelease(lease);
        var retirement = CreateRetirement();

        Assert.True(
            await retirement.RetireAsync(
                CreateSet(options => options.ClientId = "another-client"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RetireAsync_ChangedAuthority_EndsItsSessionsAsClientChanged()
    {
        var metadata = CreateMetadata(CurrentClient);
        var lease = new BffRefreshLease("retire:" + BffTestFrontends.Name, "owner");
        SetupFindClients(CurrentClient);
        SetupAcquire(BffTestFrontends.Name, lease);
        SetupFindByClient(CurrentClient, "key-1");
        SetupGet("key-1", CreateRecord("key-1", metadata));
        SetupEnd("key-1", metadata, BffSessionEndReasons.ClientChanged);
        SetupRelease(lease);
        var retirement = CreateRetirement();

        Assert.True(
            await retirement.RetireAsync(
                CreateSet(options => options.Authority = "https://other-op.example.com"),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RetireAsync_LeaseHeldElsewhere_EndsNothingAndReportsIncomplete()
    {
        var client = CurrentClient with { Frontend = RemovedFrontend };
        SetupFindClients(client);
        SetupAcquire(RemovedFrontend, null);
        var retirement = CreateRetirement();

        Assert.False(await retirement.RetireAsync(CreateSet(), CancellationToken.None));
    }

    [Fact]
    public async Task RetireAsync_RecordAlreadyGone_SkipsIt()
    {
        var client = CurrentClient with { Frontend = RemovedFrontend };
        var remaining = CreateMetadata(client);
        var lease = new BffRefreshLease("retire:" + RemovedFrontend, "owner");
        SetupFindClients(client);
        SetupAcquire(RemovedFrontend, lease);
        SetupFindByClient(client, "key-1", "key-2");
        SetupGet("key-1", null);
        SetupGet("key-2", CreateRecord("key-2", remaining));
        SetupEnd("key-2", remaining, BffSessionEndReasons.FrontendRemoved);
        SetupRelease(lease);
        var retirement = CreateRetirement();

        Assert.True(await retirement.RetireAsync(CreateSet(), CancellationToken.None));
    }

    [Fact]
    public async Task RetireAsync_EndingASessionFails_EndsTheOthersAndReportsIncomplete()
    {
        var client = CurrentClient with { Frontend = RemovedFrontend };
        var first = CreateMetadata(client);
        var second = CreateMetadata(client);
        var lease = new BffRefreshLease("retire:" + RemovedFrontend, "owner");
        var logger = new FakeLogger<BffSessionRetirement>();
        SetupFindClients(client);
        SetupAcquire(RemovedFrontend, lease);
        SetupFindByClient(client, "key-1", "key-2");
        SetupGet("key-1", CreateRecord("key-1", first));
        SetupGet("key-2", CreateRecord("key-2", second));
        _manager
            .Setup(x =>
                x.EndAsync(
                    null,
                    "key-1",
                    first,
                    BffSessionEndReasons.FrontendRemoved,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                ValueTask.FromException(new InvalidOperationException("The end failed."))
            )
            .Verifiable();
        SetupEnd("key-2", second, BffSessionEndReasons.FrontendRemoved);
        SetupRelease(lease);
        var retirement = CreateRetirement(logger);

        var complete = await retirement.RetireAsync(CreateSet(), CancellationToken.None);

        Assert.False(complete);
        Assert.Contains(
            logger.Collector.GetSnapshot(),
            record =>
                record.Id.Id == EventIds.FrontendSessionRetirementFailed
                && record.Exception is InvalidOperationException
        );
    }

    [Fact]
    public async Task RetireAsync_Always_ReleasesTheLease()
    {
        var client = CurrentClient with { Frontend = RemovedFrontend };
        var lease = new BffRefreshLease("retire:" + RemovedFrontend, "owner");
        SetupFindClients(client);
        SetupAcquire(RemovedFrontend, lease);
        _store
            .Setup(x => x.FindByClientAsync(client, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BffStoreUnavailableException("The store is unavailable."))
            .Verifiable();
        SetupRelease(lease);
        var retirement = CreateRetirement();

        await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await retirement.RetireAsync(CreateSet(), CancellationToken.None)
        );
    }

    #endregion
}
