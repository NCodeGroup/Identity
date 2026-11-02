// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

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

    #region Services Property Tests

    [Fact]
    public async Task Services_WhenTwoFactoriesStart_KeepSeparateDataProtectionKeyRings()
    {
        await using var first = new HostApplicationFactory();
        await using var second = new HostApplicationFactory();

        var firstKeyIds = first
            .Services.GetRequiredService<IKeyManager>()
            .GetAllKeys()
            .Select(key => key.KeyId)
            .ToList();
        var secondKeyIds = second
            .Services.GetRequiredService<IKeyManager>()
            .GetAllKeys()
            .Select(key => key.KeyId)
            .ToList();

        Assert.NotEmpty(firstKeyIds);
        Assert.NotEmpty(secondKeyIds);
        Assert.Empty(firstKeyIds.Intersect(secondKeyIds));
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
