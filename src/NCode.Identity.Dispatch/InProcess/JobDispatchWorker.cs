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
using NCode.Identity.Dispatch.Logging;

namespace NCode.Identity.Dispatch.InProcess;

/// <summary>
/// Drains the <see cref="InProcessJobQueue"/> and executes each job's registered <see cref="IDispatchJobHandler{TJob}"/>
/// in its own service scope, retrying failed attempts with exponential back-off up to the per-job attempt limit.
/// </summary>
internal sealed class JobDispatchWorker(
    InProcessJobQueue queue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobDispatchWorker> logger
) : BackgroundService
{
    private InProcessJobQueue Queue { get; } = queue;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private ILogger<JobDispatchWorker> Logger { get; } = logger;

    // Cached per runtime job type: the closed IDispatchJobHandler<> service type to resolve and the delegate that
    // invokes it. Handler instances are resolved per attempt from a fresh scope so scoped dependencies bind correctly.
    private static ConcurrentDictionary<Type, JobHandlerBinding> BindingCache { get; } = new();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in Queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(item, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    private async Task ProcessAsync(DispatchWorkItem item, CancellationToken stoppingToken)
    {
        var jobType = item.Job.GetType();
        var binding = GetBinding(jobType);

        using var scope = ServiceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(binding.ServiceType);

        var context = new DispatchContext
        {
            DispatchId = item.DispatchId,
            Attempt = item.Attempt,
            MaxAttempts = item.Options.MaxAttempts,
        };

        var any = false;
        foreach (var handler in handlers)
        {
            if (handler is null)
                continue;

            any = true;
            try
            {
                await binding.Invoker(handler, item.Job, context, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Reschedule(item, jobType, exception, stoppingToken);
            }
        }

        if (!any)
        {
            Logger.NoJobHandlerRegistered(
                binding.ServiceType.Name,
                jobType.FullName ?? jobType.Name
            );
        }
    }

    // Schedules a retry with exponential back-off, or abandons the job once its attempts are exhausted.
    private void Reschedule(
        DispatchWorkItem item,
        Type jobType,
        Exception exception,
        CancellationToken stoppingToken
    )
    {
        var typeName = jobType.FullName ?? jobType.Name;

        if (item.Attempt >= item.Options.MaxAttempts)
        {
            Logger.JobAttemptsExhausted(
                typeName,
                item.Attempt,
                item.Options.MaxAttempts,
                exception
            );
            return;
        }

        Logger.JobHandlerFailed(typeName, item.Attempt, item.Options.MaxAttempts, exception);

        var backoff = ComputeBackoff(item.Options, item.Attempt);
        var next = item with { Attempt = item.Attempt + 1 };
        _ = Queue.EnqueueAfterAsync(next, backoff, stoppingToken);
    }

    private static TimeSpan ComputeBackoff(DispatchOptions options, int attempt)
    {
        var factor = Math.Pow(options.BackoffFactor, attempt - 1);
        var ticks = options.InitialBackoff.Ticks * factor;
        if (ticks >= options.MaxBackoff.Ticks)
            return options.MaxBackoff;

        return TimeSpan.FromTicks((long)ticks);
    }

    private static JobHandlerBinding GetBinding(Type jobType) =>
        BindingCache.GetOrAdd(
            jobType,
            static type =>
            {
                var serviceType = typeof(IDispatchJobHandler<>).MakeGenericType(type);
                var method =
                    serviceType.GetMethod(nameof(IDispatchJobHandler<IDispatchJob>.HandleAsync))
                    ?? throw new InvalidOperationException(
                        $"The handler type '{serviceType}' does not declare a HandleAsync method."
                    );

                JobHandlerInvoker invoker = (handler, job, context, cancellationToken) =>
                    (ValueTask)(
                        method.Invoke(handler, [job, context, cancellationToken])
                        ?? throw new InvalidOperationException(
                            "The dispatch handler returned a null result."
                        )
                    );

                return new JobHandlerBinding(serviceType, invoker);
            }
        );

    private sealed record JobHandlerBinding(Type ServiceType, JobHandlerInvoker Invoker);
}
