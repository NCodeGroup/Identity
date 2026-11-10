// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class PlatformSecretProtectorRefreshServiceTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static TimeSpan Wait { get; } = TimeSpan.FromSeconds(10);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private Lock Gate { get; } = new();
        private List<(LogLevel Level, EventId Id)> EventList { get; } = [];

        public IReadOnlyList<(LogLevel Level, EventId Id)> Events
        {
            get
            {
                lock (Gate)
                    return [.. EventList];
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            lock (Gate)
                EventList.Add((logLevel, eventId));
        }
    }

    private sealed record Fixture(
        PlatformSecretProtectorRefreshService Service,
        FakeRingProvider Provider,
        PlatformSecretProtectorHolder Holder,
        FakeTimeProvider Time,
        CapturingLogger<PlatformSecretProtectorRefreshService> Logger,
        TimeSpan Interval
    );

    private Fixture CreateFixture(bool loads = true)
    {
        var provider = new FakeRingProvider();
        var holder = new PlatformSecretProtectorHolder();
        var time = new FakeTimeProvider(Now);
        var options = Options.Create(new SecretProtectionOptions());

        // A fixture that never loads reads no ring.
        var keyManagement = new KeyManagementOptions();
        if (loads)
        {
            var repository = CreateStrictMock<IXmlRepository>();
            repository.Setup(x => x.GetAllElements()).Returns([]).Verifiable();
            keyManagement.XmlRepository = repository.Object;
        }

        var loader = new PlatformSecretProtectorLoader(
            provider,
            new DataProtectionKeyRingReader(Options.Create(keyManagement)),
            options,
            time,
            holder,
            CreateLooseMock<ILogger<PlatformSecretProtectorLoader>>().Object
        );

        var logger = new CapturingLogger<PlatformSecretProtectorRefreshService>();
        var service = new PlatformSecretProtectorRefreshService(loader, options, time, logger);

        return new Fixture(
            service,
            provider,
            holder,
            time,
            logger,
            options.Value.MasterKeys.RefreshInterval
        );
    }

    private static async ValueTask<bool> UntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken
    )
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                return false;
            await Task.Delay(10, cancellationToken);
        }

        return true;
    }

    // The service may create its timer after StartAsync returns, so the clock advances until a load happens.
    private static async ValueTask<bool> TickUntilLoadAsync(
        Fixture fixture,
        CancellationToken cancellationToken
    )
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            fixture.Time.Advance(fixture.Interval);
            if (
                await fixture.Provider.Requested.WaitAsync(
                    TimeSpan.FromMilliseconds(50),
                    cancellationToken
                )
            )
                return true;
        }

        return false;
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_EachRefreshInterval_LoadsTheProtectorAgain()
    {
        var fixture = CreateFixture();

        await fixture.Service.StartAsync(CancellationToken.None);

        // No load happens before the first tick.
        Assert.Empty(fixture.Provider.Requests);
        Assert.Null(fixture.Holder.Protector);

        Assert.True(await TickUntilLoadAsync(fixture, CancellationToken.None));
        Assert.True(
            await UntilAsync(() => fixture.Holder.Protector is not null, CancellationToken.None)
        );
        var first = fixture.Holder.Protector;

        Assert.True(await TickUntilLoadAsync(fixture, CancellationToken.None));
        Assert.True(
            await UntilAsync(
                () => !ReferenceEquals(fixture.Holder.Protector, first),
                CancellationToken.None
            )
        );

        Assert.True(fixture.Provider.Requests.Count >= 2);

        await fixture.Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ALoadThatFails_IsLoggedAndTheNextTickLoadsAgain()
    {
        var fixture = CreateFixture();
        fixture.Provider.Failure = new InvalidOperationException("The store is down.");

        await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(await TickUntilLoadAsync(fixture, CancellationToken.None));
        Assert.True(
            await UntilAsync(
                () =>
                    fixture.Logger.Events.Any(e =>
                        e.Id == EventIds.PlatformSecretProtectorRefreshFailed
                    ),
                CancellationToken.None
            )
        );
        Assert.All(fixture.Logger.Events, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.False(fixture.Service.ExecuteTask?.IsCompleted);
        Assert.Null(fixture.Holder.Protector);

        fixture.Provider.Failure = null;
        Assert.True(await TickUntilLoadAsync(fixture, CancellationToken.None));
        Assert.True(
            await UntilAsync(() => fixture.Holder.Protector is not null, CancellationToken.None)
        );

        await fixture.Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Stopping_EndsQuietly()
    {
        var fixture = CreateFixture(loads: false);

        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.StopAsync(CancellationToken.None);

        Assert.True(fixture.Service.ExecuteTask is { IsCompleted: true, IsFaulted: false });
        Assert.Empty(fixture.Logger.Events);
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    public async Task RefreshAsync_WhenCancelled_Throws()
    {
        var fixture = CreateFixture();
        fixture.Provider.Failure = new OperationCanceledException();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Service.RefreshAsync(cts.Token).AsTask()
        );

        Assert.Empty(fixture.Logger.Events);
    }

    #endregion
}
