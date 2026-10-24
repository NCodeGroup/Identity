#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.InProcess;
using NCode.Identity.Dispatch.Options;
using Xunit;

namespace NCode.Identity.Dispatch;

public class InProcessJobDispatchTests
{
    private static readonly DispatchOptions FastOptions = new()
    {
        MaxAttempts = 3,
        InitialBackoff = TimeSpan.FromMilliseconds(1),
        BackoffFactor = 1.0,
        MaxBackoff = TimeSpan.FromMilliseconds(5),
    };

    private sealed record RecordingJob : IDispatchJob
    {
        public required string Name { get; init; }
    }

    private sealed class RecordingHandler(int throwTimes) : IDispatchJobHandler<RecordingJob>
    {
        private readonly TaskCompletionSource _succeeded = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly TaskCompletionSource _finalAttempt = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public List<DispatchContext> Attempts { get; } = [];
        public Task Succeeded => _succeeded.Task;
        public Task FinalAttemptStarted => _finalAttempt.Task;

        public ValueTask HandleAsync(
            RecordingJob job,
            DispatchContext context,
            CancellationToken cancellationToken
        )
        {
            Attempts.Add(context);

            if (context.Attempt == context.MaxAttempts)
                _finalAttempt.TrySetResult();

            if (context.Attempt <= throwTimes)
                throw new InvalidOperationException("boom");

            _succeeded.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailsFirstAttemptHandler(Exception failure)
        : IDispatchJobHandler<RecordingJob>
    {
        public ConcurrentQueue<int> Attempts { get; } = [];
        public TaskCompletionSource Retried { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask HandleAsync(
            RecordingJob job,
            DispatchContext context,
            CancellationToken cancellationToken
        )
        {
            Attempts.Enqueue(context.Attempt);

            if (context.Attempt == 1)
                return ValueTask.FromException(failure);

            Retried.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record GatedJob : IDispatchJob
    {
        public required bool WaitsForRelease { get; init; }
    }

    private sealed class GatedHandler : IDispatchJobHandler<GatedJob>
    {
        public TaskCompletionSource GatedStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource UngatedCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask HandleAsync(
            GatedJob job,
            DispatchContext context,
            CancellationToken cancellationToken
        )
        {
            if (!job.WaitsForRelease)
            {
                UngatedCompleted.TrySetResult();
                return;
            }

            GatedStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }

    private static async Task<(
        ServiceProvider Provider,
        JobDispatchWorker Worker,
        IJobDispatcher Dispatcher
    )> StartAsync(Action<IServiceCollection> registerHandlers, int? maxConcurrentJobs = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddOptions();
        serviceCollection.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        serviceCollection.AddSingleton<InProcessJobQueue>();
        serviceCollection.AddSingleton<IJobDispatcher, DefaultInProcessJobDispatcher>();

        registerHandlers(serviceCollection);

        if (maxConcurrentJobs is { } value)
        {
            serviceCollection.Configure<InProcessJobDispatcherOptions>(options =>
                options.MaxConcurrentJobs = value
            );
        }

        var provider = serviceCollection.BuildServiceProvider();

        var worker = new JobDispatchWorker(
            provider.GetRequiredService<InProcessJobQueue>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<InProcessJobDispatcherOptions>>(),
            provider.GetRequiredService<ILogger<JobDispatchWorker>>()
        );
        await worker.StartAsync(CancellationToken.None);

        return (provider, worker, provider.GetRequiredService<IJobDispatcher>());
    }

    private static Task<(
        ServiceProvider Provider,
        JobDispatchWorker Worker,
        IJobDispatcher Dispatcher
    )> StartAsync<TJob>(
        IReadOnlyList<IDispatchJobHandler<TJob>> handlers,
        int? maxConcurrentJobs = null
    )
        where TJob : class, IDispatchJob =>
        StartAsync(
            serviceCollection =>
            {
                foreach (var handler in handlers)
                {
                    serviceCollection.AddSingleton<IDispatchJobHandler<TJob>>(handler);
                }
            },
            maxConcurrentJobs
        );

    private static Task<(
        ServiceProvider Provider,
        JobDispatchWorker Worker,
        IJobDispatcher Dispatcher
    )> StartAsync<TJob>(IDispatchJobHandler<TJob> handler, int? maxConcurrentJobs = null)
        where TJob : class, IDispatchJob => StartAsync<TJob>([handler], maxConcurrentJobs);

    #region DispatchAsync Tests

    [Fact]
    public async Task DispatchAsync_WhenHandlerSucceeds_InvokesHandlerOnce()
    {
        var handler = new RecordingHandler(throwTimes: 0);
        var (provider, worker, dispatcher) = await StartAsync(handler);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.Succeeded.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Single(handler.Attempts);
        Assert.Equal(1, handler.Attempts[0].Attempt);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrowsThenSucceeds_RetriesUntilSuccess()
    {
        var handler = new RecordingHandler(throwTimes: 1);
        var (provider, worker, dispatcher) = await StartAsync(handler);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.Succeeded.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Equal(2, handler.Attempts.Count);
        Assert.Equal([1, 2], handler.Attempts.Select(x => x.Attempt));
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerAlwaysThrows_StopsAfterMaxAttempts()
    {
        var handler = new RecordingHandler(throwTimes: int.MaxValue);
        var (provider, worker, dispatcher) = await StartAsync(handler);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.FinalAttemptStarted.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Equal(FastOptions.MaxAttempts, handler.Attempts.Count);
    }

    [Fact]
    public async Task DispatchAsync_AcrossRetries_KeepsSameDispatchId()
    {
        var handler = new RecordingHandler(throwTimes: 2);
        var (provider, worker, dispatcher) = await StartAsync(handler);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.Succeeded.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Equal(3, handler.Attempts.Count);
        var dispatchId = handler.Attempts[0].DispatchId;
        Assert.NotEqual(Guid.Empty, dispatchId);
        Assert.All(handler.Attempts, context => Assert.Equal(dispatchId, context.DispatchId));
    }

    [Fact]
    public async Task DispatchAsync_WhenAJobWaits_RunsTheNextJobAlongsideIt()
    {
        var handler = new GatedHandler();
        var (provider, worker, dispatcher) = await StartAsync(handler, maxConcurrentJobs: 2);

        await dispatcher.DispatchAsync(new GatedJob { WaitsForRelease = true }, FastOptions);
        await dispatcher.DispatchAsync(new GatedJob { WaitsForRelease = false }, FastOptions);
        await handler.GatedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await handler.UngatedCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        handler.Release.TrySetResult();
        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task DispatchAsync_WhenEveryConsumerIsBusy_WaitsForOneToFinish()
    {
        var handler = new GatedHandler();
        var (provider, worker, dispatcher) = await StartAsync(handler, maxConcurrentJobs: 1);

        await dispatcher.DispatchAsync(new GatedJob { WaitsForRelease = true }, FastOptions);
        await dispatcher.DispatchAsync(new GatedJob { WaitsForRelease = false }, FastOptions);
        await handler.GatedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // A short wait that a worker without a limit would not survive.
        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.UngatedCompleted.Task.WaitAsync(TimeSpan.FromMilliseconds(250))
        );

        handler.Release.TrySetResult();
        await handler.UngatedCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerTimesOut_RetriesTheJob()
    {
        var handler = new FailsFirstAttemptHandler(
            new TaskCanceledException("The request timed out.")
        );
        var (provider, worker, dispatcher) = await StartAsync(handler);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.Retried.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Equal([1, 2], handler.Attempts);
    }

    [Fact]
    public async Task DispatchAsync_WhenTwoHandlersFail_RetriesTheJobOnce()
    {
        var first = new FailsFirstAttemptHandler(new InvalidOperationException("first"));
        var second = new FailsFirstAttemptHandler(new InvalidOperationException("second"));
        var (provider, worker, dispatcher) = await StartAsync<RecordingJob>([first, second]);

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await first.Retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await second.Retried.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // A second retry of the same attempt would arrive within the 5 ms back-off.
        await Task.Delay(TimeSpan.FromMilliseconds(250));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.Equal([1, 2], first.Attempts);
        Assert.Equal([1, 2], second.Attempts);
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerCannotBeResolved_RetriesTheJob()
    {
        var handler = new RecordingHandler(throwTimes: 0);
        var resolutions = 0;
        var (provider, worker, dispatcher) = await StartAsync(
            serviceCollection =>
                serviceCollection.AddScoped<IDispatchJobHandler<RecordingJob>>(_ =>
                    Interlocked.Increment(ref resolutions) == 1
                        ? throw new InvalidOperationException("The handler cannot be resolved.")
                        : handler
                ),
            maxConcurrentJobs: 1
        );

        await dispatcher.DispatchAsync(new RecordingJob { Name = "value" }, FastOptions);
        await handler.Succeeded.WaitAsync(TimeSpan.FromSeconds(5));

        await worker.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();

        // The first attempt failed before any handler ran, and the only consumer survived to run the retry.
        Assert.Equal([2], handler.Attempts.Select(x => x.Attempt));
    }

    #endregion
}
