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

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.InProcess;
using NCode.Identity.Dispatch.Options;
using Xunit;

namespace NCode.Identity.Dispatch;

public class DispatchRegistrationTests
{
    private sealed record SampleJob : IDispatchJob;

    private sealed class SampleHandler : IDispatchJobHandler<SampleJob>
    {
        public ValueTask HandleAsync(
            SampleJob job,
            DispatchContext context,
            CancellationToken cancellationToken
        ) => ValueTask.CompletedTask;
    }

    #region AddInProcessJobDispatcher Tests

    [Fact]
    public void AddInProcessJobDispatcher_RegistersTheDefaultAdapterAndWorker()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IHostApplicationLifetime, FakeLifetime>();

        serviceCollection.AddDispatchServices().AddInProcessJobDispatcher();

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<DefaultInProcessJobDispatcher>(provider.GetRequiredService<IJobDispatcher>());
        Assert.Single(provider.GetServices<IHostedService>().OfType<JobDispatchWorker>());
    }

    [Fact]
    public void AddInProcessJobDispatcher_WhenCustomDispatcherRegisteredFirst_KeepsCustomAdapter()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IHostApplicationLifetime, FakeLifetime>();
        serviceCollection.AddSingleton<IJobDispatcher, CustomDispatcher>();

        serviceCollection.AddDispatchServices().AddInProcessJobDispatcher();

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<CustomDispatcher>(provider.GetRequiredService<IJobDispatcher>());
    }

    [Fact]
    public void AddInProcessJobDispatcher_WhenConfigured_AppliesQueueOptions()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IHostApplicationLifetime, FakeLifetime>();

        serviceCollection
            .AddDispatchServices()
            .AddInProcessJobDispatcher(options =>
            {
                options.QueueCapacity = 7;
                options.FullMode = BoundedChannelFullMode.DropOldest;
            });

        using var provider = serviceCollection.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InProcessJobDispatcherOptions>>().Value;
        Assert.Equal(7, options.QueueCapacity);
        Assert.Equal(BoundedChannelFullMode.DropOldest, options.FullMode);
    }

    #endregion

    #region AddDispatchJobHandler Tests

    [Fact]
    public void AddDispatchJobHandler_RegistersTheHandlerForItsJobType()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddDispatchServices().AddDispatchJobHandler<SampleJob, SampleHandler>();

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<SampleHandler>(
            Assert.Single(provider.GetServices<IDispatchJobHandler<SampleJob>>())
        );
    }

    #endregion

    private sealed class CustomDispatcher : IJobDispatcher
    {
        public ValueTask DispatchAsync<TJob>(
            TJob job,
            DispatchOptions? options = null,
            CancellationToken cancellationToken = default
        )
            where TJob : class, IDispatchJob => ValueTask.CompletedTask;
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }
}
