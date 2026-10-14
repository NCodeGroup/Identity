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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Identity.Dispatch.InProcess;
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
    )> StartAsync(RecordingHandler handler)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddOptions();
        serviceCollection.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        serviceCollection.AddSingleton<InProcessJobQueue>();
        serviceCollection.AddSingleton<IJobDispatcher, DefaultInProcessJobDispatcher>();
        serviceCollection.AddSingleton<IDispatchJobHandler<RecordingJob>>(handler);

        var provider = serviceCollection.BuildServiceProvider();

        var worker = new JobDispatchWorker(
            provider.GetRequiredService<InProcessJobQueue>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<JobDispatchWorker>>()
        );
        await worker.StartAsync(CancellationToken.None);

        return (provider, worker, provider.GetRequiredService<IJobDispatcher>());
    }

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

    #endregion
}
