// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hosting;

public sealed class BffSessionRetirementServiceTests : BaseTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly BffTestOptionsSource _source;
    private readonly BffFrontendCatalog _frontends;
    private readonly Mock<BffSessionRetirement> _retirement;
    private readonly Channel<BffFrontendSet> _passes = Channel.CreateUnbounded<BffFrontendSet>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly FakeLogger<BffSessionRetirementService> _logger = new();

    public BffSessionRetirementServiceTests()
    {
        _source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        _frontends = BffTestFrontends.CreateCatalog(_source);
        _retirement = CreateStrictMock<BffSessionRetirement>(
            BffTestTelemetry.Instrument(CreateStrictMock<IBffSessionStore>().Object),
            CreateStrictMock<IBffSessionManager>().Object,
            _timeProvider,
            NullLogger<BffSessionRetirement>.Instance
        );
    }

    private BffSessionRetirementService CreateService() =>
        new(_frontends, _retirement.Object, _timeProvider, _logger);

    private void SetupRetire(Func<int, bool> fails, Func<int, bool>? complete = null)
    {
        complete ??= _ => true;
        var calls = 0;
        _retirement
            .Setup(x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()))
            .Returns(
                (BffFrontendSet set, CancellationToken _) =>
                {
                    var call = Interlocked.Increment(ref calls);
                    _passes.Writer.TryWrite(set);
                    return fails(call)
                        ? throw new BffStoreUnavailableException("The store is unavailable.")
                        : ValueTask.FromResult(complete(call));
                }
            )
            .Verifiable();
    }

    private async Task<BffFrontendSet> ReadPassAsync() =>
        await _passes.Reader.ReadAsync().AsTask().WaitAsync(WaitLimit);

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_Start_RetiresAgainstTheCurrentSet()
    {
        SetupRetire(_ => false);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var set = await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Same(_frontends.Current, set);
        _retirement.Verify(
            x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_AfterAChange_RetiresAgainstTheChangedSet()
    {
        SetupRetire(_ => false);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var first = await ReadPassAsync();
        _source.Change(
            BffTestFrontends.CreateBffOptions(options =>
                options.Frontends["other"] = BffTestFrontends.CreateOptions(frontend =>
                {
                    frontend.ClientId = "other";
                    frontend.PublicOrigin = "https://other.example";
                })
            )
        );
        var second = await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Same(_frontends.Current, second);
        Assert.Contains(second.Frontends, frontend => frontend.Name == "other");
    }

    [Fact]
    public async Task ExecuteAsync_StoreFails_RetriesAfterTheDelay()
    {
        SetupRetire(call => call == 1);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await ReadPassAsync();
        var retried = ReadPassAsync();
        while (!retried.IsCompleted)
        {
            _timeProvider.Advance(BffSessionRetirementService.RetryDelay);
            await Task.Delay(10);
        }

        await retried;
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(
            _logger.Collector.GetSnapshot(),
            record =>
                record.Id.Id == EventIds.FrontendRetirementFailed
                && record.Exception is BffStoreUnavailableException
        );
        _retirement.Verify(
            x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task ExecuteAsync_LeaseLeftToAnotherHolder_RetriesAfterTheDelay()
    {
        SetupRetire(_ => false, call => call != 1);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await ReadPassAsync();
        var retried = ReadPassAsync();
        while (!retried.IsCompleted)
        {
            _timeProvider.Advance(BffSessionRetirementService.RetryDelay);
            await Task.Delay(10);
        }

        await retried;
        await service.StopAsync(CancellationToken.None);

        _retirement.Verify(
            x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    #endregion
}
