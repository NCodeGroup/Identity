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
    private static DateTimeOffset Now { get; } = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private RemoteKeyOptions KeyOptions { get; } = new() { Deployment = "acme" };
    private FakeTimeProvider Time { get; } = new(Now);
    private Mock<IMaterializedPlatformAccessor> PlatformAccessor { get; }
    private Mock<IRemoteKeyDescriptorCatalog> Catalog { get; }
    private Mock<IJobDispatcher> Dispatcher { get; }
    private Mock<IExpiringKeySet> KeySet { get; }
    private Mock<OpenIdPlatform> Platform { get; }
    private RemoteKeySweepService Service { get; }
    private List<RemoteKeySweepJob> Dispatched { get; } = [];

    public RemoteKeySweepServiceTests()
    {
        PlatformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        Catalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
        Dispatcher = CreateStrictMock<IJobDispatcher>();
        KeySet = CreateStrictMock<IExpiringKeySet>();
        Platform = CreateStrictMock<OpenIdPlatform>();

        Service = CreateService(CreateLooseMock<ILogger<RemoteKeySweepService>>().Object);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Service.Dispose();

        base.Dispose(disposing);
    }

    private RemoteKeySweepService CreateService(ILogger<RemoteKeySweepService> logger) =>
        new(
            PlatformAccessor.Object,
            Catalog.Object,
            Dispatcher.Object,
            KeySet.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RemoteKeys = KeyOptions }
            ),
            Time,
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

    private void SetupProviders(params string[] names) => SetupProviders(names, []);

    private void SetupProviders(string[] names, string[] namesThatCreateNoKeys) =>
        Catalog
            .Setup(x => x.Providers)
            .Returns(
                names
                    .Concat(namesThatCreateNoKeys)
                    .Select(name => new RemoteKeyProviderDescriptor
                    {
                        Name = name,
                        CreatesKeys = !namesThatCreateNoKeys.Contains(name),
                    })
                    .ToArray()
            )
            .Verifiable();

    private void SetupLease(bool acquired) =>
        KeySet
            .Setup(x =>
                x.TryAddAsync(
                    RemoteKeySweepService.LeaseKey,
                    Now + KeyOptions.SweepInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable(Times.Once());

    private void SetupDispatch() =>
        Dispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.IsAny<RemoteKeySweepJob>(),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<RemoteKeySweepJob, DispatchOptions?, CancellationToken>(
                (job, _, _) => Dispatched.Add(job)
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

        await Service.RunAsync(CancellationToken.None);

        Assert.Equal(["aws", "azure", "google"], Dispatched.Select(job => job.Provider));
    }

    [Fact]
    public async Task RunAsync_WhenAProviderCreatesNoKeys_DispatchesNoJobForIt()
    {
        SetupProviders(["google", "azure"], ["aws"]);
        SetupLease(true);
        SetupDispatch();

        await Service.RunAsync(CancellationToken.None);

        Assert.Equal(["azure", "google"], Dispatched.Select(job => job.Provider));
    }

    [Fact]
    public async Task RunAsync_WhenTheLeaseIsTaken_DispatchesNothing()
    {
        SetupLease(false);

        await Service.RunAsync(CancellationToken.None);

        Assert.Empty(Dispatched);
        Assert.Empty(Dispatcher.Invocations);
        Assert.Empty(Catalog.Invocations);
    }

    [Fact]
    public async Task RunAsync_WhenADispatchFails_LogsTheProviderAndDispatchesTheOthers()
    {
        SetupProviders("aws", "google");
        SetupLease(true);
        SetupDispatch();
        Dispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.Is<RemoteKeySweepJob>(job => job.Provider == "aws"),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable(Times.Once());
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(["google"], Dispatched.Select(job => job.Provider));
        Assert.Equal(
            [(LogLevel.Warning, EventIds.RemoteKeySweepDispatchFailed)],
            entries.Where(entry => entry.Level >= LogLevel.Warning)
        );
    }

    [Fact]
    public async Task RunAsync_WhenTheLeaseFails_LogsThePassFailure()
    {
        KeySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new InvalidOperationException("boom"))
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
        KeySet
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
            await Service.RunAsync(cancelled.Token)
        );
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenTheSweepIsDisabled_ReturnsWithoutWaitingForThePlatform()
    {
        KeyOptions.SweepEnabled = false;

        await Service.StartAsync(CancellationToken.None);
        await Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Service.StopAsync(CancellationToken.None);

        Assert.Empty(PlatformAccessor.Invocations);
        Assert.Empty(KeySet.Invocations);
        Assert.Empty(Dispatcher.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoProviderIsDescribed_ReturnsWithoutWaitingForThePlatform()
    {
        SetupProviders();

        await Service.StartAsync(CancellationToken.None);
        await Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Service.StopAsync(CancellationToken.None);

        Assert.Empty(PlatformAccessor.Invocations);
        Assert.Empty(KeySet.Invocations);
        Assert.Empty(Dispatcher.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoProviderCreatesKeys_ReturnsWithoutWaitingForThePlatform()
    {
        SetupProviders([], ["aws"]);

        await Service.StartAsync(CancellationToken.None);
        await Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Service.StopAsync(CancellationToken.None);

        Assert.Empty(PlatformAccessor.Invocations);
        Assert.Empty(KeySet.Invocations);
        Assert.Empty(Dispatcher.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_EverySweepInterval_RunsAPassUntilStopped()
    {
        SetupProviders("aws");
        PlatformAccessor
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OpenIdPlatform>(Platform.Object))
            .Verifiable(Times.Once());

        // The lease answers false, so a pass takes the lease and does nothing else.
        using var leased = new SemaphoreSlim(0);
        KeySet
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

        await Service.StartAsync(CancellationToken.None);

        // The service may create its timer after StartAsync returns, so the clock advances until the second pass.
        Assert.True(await leased.WaitAsync(TimeSpan.FromSeconds(10)));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var ticked = false;
        while (!ticked && DateTime.UtcNow < deadline)
        {
            Time.Advance(KeyOptions.SweepInterval);
            ticked = await leased.WaitAsync(TimeSpan.FromMilliseconds(50));
        }

        Assert.True(ticked);

        await Service.StopAsync(CancellationToken.None);

        Assert.True(Service.ExecuteTask is { IsCompleted: true, IsFaulted: false });
    }

    #endregion
}
