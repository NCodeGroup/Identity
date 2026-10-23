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

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// Provides unit tests for the <see cref="HostApplicationFactory"/> class.
/// </summary>
public class HostApplicationFactoryTests
{
    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_WhenTheHostRan_StopsEachHostedServiceOnce()
    {
        var service = new StopCountingService();
        await using var factory = new HostApplicationFactory();
        var derived = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection.AddSingleton<IHostedService>(service)
            )
        );

        _ = derived.Services;
        await derived.DisposeAsync();

        Assert.Equal(1, service.StopCalls);
    }

    #endregion

    private sealed class StopCountingService : IHostedService
    {
        private int _stopCalls;

        public int StopCalls => Volatile.Read(ref _stopCalls);

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _stopCalls);
            return Task.CompletedTask;
        }
    }
}
