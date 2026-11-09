// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class RefreshableSettingsCollectionDataSourceTests : BaseTests
{
    private static TimeSpan Interval { get; } = TimeSpan.FromMinutes(5.0);
    private static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(5.0);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = [OpenIdSettingScopes.Platform] };

    private TimerCallback? TimerCallback { get; set; }
    private object? TimerState { get; set; }

    private static IReadOnlyCollection<Setting> CreateSettings(bool value) =>
        [new Setting<bool>(FlagDescriptor, value)];

    private RefreshableSettingsCollectionDataSource CreateDataSource(
        IReadOnlyCollection<Setting> initialSettings,
        Func<CancellationToken, ValueTask<IReadOnlyCollection<Setting>?>> loadChangedSettingsAsync
    )
    {
        var mockTimer = CreateStrictMock<ITimer>();
        mockTimer.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(x =>
                x.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(), Interval, Interval)
            )
            .Callback(
                (TimerCallback callback, object? state, TimeSpan _, TimeSpan _) =>
                {
                    TimerCallback = callback;
                    TimerState = state;
                }
            )
            .Returns(mockTimer.Object)
            .Verifiable();

        return new RefreshableSettingsCollectionDataSource(
            initialSettings,
            loadChangedSettingsAsync,
            Interval,
            mockTimeProvider.Object,
            CreateLooseMock<ILogger<RefreshableSettingsCollectionDataSource>>().Object
        );
    }

    #region RefreshAsync Tests

    [Fact]
    public async Task RefreshAsync_WhenTheSettingsChanged_ReplacesTheCollectionAndSignalsTheChange()
    {
        var initialSettings = CreateSettings(true);
        var changedSettings = CreateSettings(false);
        await using var dataSource = CreateDataSource(
            initialSettings,
            _ => ValueTask.FromResult<IReadOnlyCollection<Setting>?>(changedSettings)
        );
        IEnumerable<Setting>? observedAtSignal = null;
        var changeToken = dataSource.GetChangeToken();
        using var registration = changeToken.RegisterChangeCallback(
            _ => observedAtSignal = dataSource.Collection,
            null
        );

        await dataSource.RefreshAsync(CancellationToken.None);

        Assert.Same(changedSettings, dataSource.Collection);
        Assert.Same(changedSettings, observedAtSignal);
        Assert.True(changeToken.HasChanged);
        Assert.False(dataSource.GetChangeToken().HasChanged);
    }

    [Fact]
    public async Task RefreshAsync_WhenTheSettingsAreUnchanged_KeepsTheCollection()
    {
        var initialSettings = CreateSettings(true);
        var loadCount = 0;
        await using var dataSource = CreateDataSource(
            initialSettings,
            _ =>
            {
                loadCount++;
                return ValueTask.FromResult<IReadOnlyCollection<Setting>?>(null);
            }
        );
        var changeToken = dataSource.GetChangeToken();

        await dataSource.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, loadCount);
        Assert.Same(initialSettings, dataSource.Collection);
        Assert.False(changeToken.HasChanged);
    }

    [Fact]
    public async Task RefreshAsync_AfterDisposeAsync_DoesNotLoadTheSettings()
    {
        var initialSettings = CreateSettings(true);
        var loadCount = 0;
        var dataSource = CreateDataSource(
            initialSettings,
            _ =>
            {
                loadCount++;
                return ValueTask.FromResult<IReadOnlyCollection<Setting>?>(CreateSettings(false));
            }
        );
        await dataSource.DisposeAsync();

        await dataSource.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, loadCount);
        Assert.Same(initialSettings, dataSource.Collection);
    }

    #endregion

    #region Timer Tests

    [Fact]
    public async Task Timer_WhenItFires_RefreshesTheSettings()
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var dataSource = CreateDataSource(
            CreateSettings(true),
            _ =>
            {
                loaded.TrySetResult();
                return ValueTask.FromResult<IReadOnlyCollection<Setting>?>(null);
            }
        );
        Assert.NotNull(TimerCallback);

        TimerCallback(TimerState);

        await loaded.Task.WaitAsync(Deadline);
    }

    [Fact]
    public async Task Timer_WhenARefreshFails_KeepsTheCollection()
    {
        var initialSettings = CreateSettings(true);
        var attempted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var dataSource = CreateDataSource(
            initialSettings,
            _ =>
            {
                attempted.TrySetResult();
                throw new InvalidOperationException("The store is unavailable.");
            }
        );
        Assert.NotNull(TimerCallback);

        TimerCallback(TimerState);

        await attempted.Task.WaitAsync(Deadline);
        await dataSource.DisposeAsync();
        Assert.Same(initialSettings, dataSource.Collection);
    }

    #endregion
}
