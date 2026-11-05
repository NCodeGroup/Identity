// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Logging;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ServerSecretProtectorRefreshServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly object _gate = new();
        private readonly List<(LogLevel Level, EventId Id)> _events = [];

        public IReadOnlyList<(LogLevel Level, EventId Id)> Events
        {
            get
            {
                lock (_gate)
                    return [.. _events];
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
            lock (_gate)
                _events.Add((logLevel, eventId));
        }
    }

    private sealed record Fixture(
        ServerSecretProtectorRefreshService Service,
        FakeRingProvider Provider,
        ServerSecretProtectorHolder Holder,
        FakeTimeProvider Time,
        CapturingLogger<ServerSecretProtectorRefreshService> Logger,
        TimeSpan Interval
    );

    private Fixture CreateFixture(bool loads = true)
    {
        var provider = new FakeRingProvider();
        var holder = new ServerSecretProtectorHolder();
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

        var loader = new ServerSecretProtectorLoader(
            provider,
            new DataProtectionKeyRingReader(Options.Create(keyManagement)),
            Options.Create(
                new OpenIdOptions { Server = new OpenIdServerOptions { ServerId = "server-a" } }
            ),
            options,
            time,
            holder,
            CreateLooseMock<ILogger<ServerSecretProtectorLoader>>().Object
        );

        var logger = new CapturingLogger<ServerSecretProtectorRefreshService>();
        var service = new ServerSecretProtectorRefreshService(loader, options, time, logger);

        return new Fixture(
            service,
            provider,
            holder,
            time,
            logger,
            options.Value.MasterKeys.RefreshInterval
        );
    }

    private static async Task<bool> UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                return false;
            await Task.Delay(10);
        }

        return true;
    }

    // The service may create its timer after StartAsync returns, so the clock advances until a load happens.
    private static async Task<bool> TickUntilLoadAsync(Fixture fixture)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            fixture.Time.Advance(fixture.Interval);
            if (await fixture.Provider.Requested.WaitAsync(TimeSpan.FromMilliseconds(50)))
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

        Assert.True(await TickUntilLoadAsync(fixture));
        Assert.True(await UntilAsync(() => fixture.Holder.Protector is not null));
        var first = fixture.Holder.Protector;

        Assert.True(await TickUntilLoadAsync(fixture));
        Assert.True(await UntilAsync(() => !ReferenceEquals(fixture.Holder.Protector, first)));

        Assert.True(fixture.Provider.Requests.Count >= 2);

        await fixture.Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ALoadThatFails_IsLoggedAndTheNextTickLoadsAgain()
    {
        var fixture = CreateFixture();
        fixture.Provider.Failure = new InvalidOperationException("The store is down.");

        await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(await TickUntilLoadAsync(fixture));
        Assert.True(
            await UntilAsync(() =>
                fixture.Logger.Events.Any(e => e.Id == EventIds.ServerSecretProtectorRefreshFailed)
            )
        );
        Assert.All(fixture.Logger.Events, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.False(fixture.Service.ExecuteTask?.IsCompleted);
        Assert.Null(fixture.Holder.Protector);

        fixture.Provider.Failure = null;
        Assert.True(await TickUntilLoadAsync(fixture));
        Assert.True(await UntilAsync(() => fixture.Holder.Protector is not null));

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
