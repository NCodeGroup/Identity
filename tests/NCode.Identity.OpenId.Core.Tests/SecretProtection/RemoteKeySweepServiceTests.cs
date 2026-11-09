// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class RemoteKeySweepServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly RemoteKeyOptions _keyOptions = new() { Deployment = "acme" };
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<IMaterializedPlatformAccessor> _platformAccessor;
    private readonly Mock<IRemoteKeyDescriptorCatalog> _catalog;
    private readonly Mock<IJobDispatcher> _dispatcher;
    private readonly Mock<IExpiringKeySet> _keySet;
    private readonly Mock<OpenIdPlatform> _platform;
    private readonly RemoteKeySweepService _service;
    private readonly List<RemoteKeySweepJob> _dispatched = [];

    public RemoteKeySweepServiceTests()
    {
        _platformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        _catalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
        _dispatcher = CreateStrictMock<IJobDispatcher>();
        _keySet = CreateStrictMock<IExpiringKeySet>();
        _platform = CreateStrictMock<OpenIdPlatform>();

        _service = CreateService(CreateLooseMock<ILogger<RemoteKeySweepService>>().Object);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _service.Dispose();

        base.Dispose(disposing);
    }

    private RemoteKeySweepService CreateService(ILogger<RemoteKeySweepService> logger) =>
        new(
            _platformAccessor.Object,
            _catalog.Object,
            _dispatcher.Object,
            _keySet.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RemoteKeys = _keyOptions }
            ),
            _time,
            logger
        );

    // A strict logger that records the level and the event id of each entry; only the tests that check a log use it.
    private ILogger<RemoteKeySweepService> CreateCapturingLogger(
        List<(LogLevel Level, int EventId)> entries
    )
    {
        var logger = CreateStrictMock<ILogger<RemoteKeySweepService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true).Verifiable();
#pragma warning disable CA1873 // The arguments are matchers of a mock setup, not log arguments.
        logger
            .Setup(x =>
                x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Callback(
                new InvocationAction(invocation =>
                {
                    if (invocation.Arguments is [LogLevel level, EventId eventId, ..])
                        entries.Add((level, eventId.Id));
                })
            )
            .Verifiable();
#pragma warning restore CA1873
        return logger.Object;
    }

    private void SetupProviders(params string[] names) =>
        _catalog
            .Setup(x => x.Providers)
            .Returns(
                names.Select(name => new RemoteKeyProviderDescriptor { Name = name }).ToArray()
            )
            .Verifiable();

    private void SetupLease(bool acquired) =>
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    RemoteKeySweepService.LeaseKey,
                    Now + _keyOptions.SweepInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable(Times.Once());

    private void SetupDispatch() =>
        _dispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<RemoteKeySweepJob>(),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<RemoteKeySweepJob, DispatchOptions?, CancellationToken>(
                (job, _, _) => _dispatched.Add(job)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_WhenTheLeaseIsWon_DispatchesOneJobPerProviderInOrder()
    {
        SetupProviders("google", "aws", "azure");
        SetupLease(true);
        SetupDispatch();

        await _service.RunAsync(CancellationToken.None);

        Assert.Equal(["aws", "azure", "google"], _dispatched.Select(job => job.Provider));
    }

    [Fact]
    public async Task RunAsync_WhenTheLeaseIsTaken_DispatchesNothing()
    {
        SetupLease(false);

        await _service.RunAsync(CancellationToken.None);

        Assert.Empty(_dispatched);
        Assert.Empty(_dispatcher.Invocations);
        Assert.Empty(_catalog.Invocations);
    }

    [Fact]
    public async Task RunAsync_WhenADispatchFails_LogsAndDoesNotThrow()
    {
        SetupProviders("aws");
        SetupLease(true);
        _dispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<RemoteKeySweepJob>(),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable(Times.Once());
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(
            [(LogLevel.Warning, EventIds.RemoteKeySweepPassFailed)],
            entries.Where(entry => entry.Level >= LogLevel.Warning)
        );
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new OperationCanceledException(cancelled.Token))
            .Verifiable(Times.Once());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _service.RunAsync(cancelled.Token)
        );
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenTheSweepIsDisabled_ReturnsWithoutWaitingForThePlatform()
    {
        _keyOptions.SweepEnabled = false;

        await _service.StartAsync(CancellationToken.None);
        await _service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await _service.StopAsync(CancellationToken.None);

        Assert.Empty(_platformAccessor.Invocations);
        Assert.Empty(_keySet.Invocations);
        Assert.Empty(_dispatcher.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoProviderIsDescribed_ReturnsWithoutWaitingForThePlatform()
    {
        SetupProviders();

        await _service.StartAsync(CancellationToken.None);
        await _service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await _service.StopAsync(CancellationToken.None);

        Assert.Empty(_platformAccessor.Invocations);
        Assert.Empty(_keySet.Invocations);
        Assert.Empty(_dispatcher.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_EverySweepInterval_RunsAPassUntilStopped()
    {
        SetupProviders("aws");
        _platformAccessor
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OpenIdPlatform>(_platform.Object))
            .Verifiable(Times.Once());

        // The lease answers false, so a pass takes the lease and does nothing else.
        using var leased = new SemaphoreSlim(0);
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    RemoteKeySweepService.LeaseKey,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => leased.Release())
            .ReturnsAsync(false)
            .Verifiable(Times.AtLeast(2));

        await _service.StartAsync(CancellationToken.None);

        // The service may create its timer after StartAsync returns, so the clock advances until the second pass.
        Assert.True(await leased.WaitAsync(TimeSpan.FromSeconds(10)));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var ticked = false;
        while (!ticked && DateTime.UtcNow < deadline)
        {
            _time.Advance(_keyOptions.SweepInterval);
            ticked = await leased.WaitAsync(TimeSpan.FromMilliseconds(50));
        }

        Assert.True(ticked);

        await _service.StopAsync(CancellationToken.None);

        Assert.True(_service.ExecuteTask is { IsCompleted: true, IsFaulted: false });
    }

    #endregion
}
