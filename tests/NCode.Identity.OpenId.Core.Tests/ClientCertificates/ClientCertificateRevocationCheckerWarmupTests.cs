// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class ClientCertificateRevocationCheckerWarmupTests : BaseTests
{
    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenTheCheckerResolves_Completes()
    {
        var checker = CreateStrictMock<IClientCertificateRevocationChecker>().Object;
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton(checker)
            .BuildServiceProvider();
        var warmup = new ClientCertificateRevocationCheckerWarmup(serviceProvider);

        await warmup.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WhenTheCheckerCannotBeConstructed_Throws()
    {
        // The sources and their descriptors do not correspond: the checker's constructor throws, and so does the start.
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton<IClientCertificateRevocationChecker>(_ =>
                throw new InvalidOperationException("No source is registered for 'denylist'.")
            )
            .BuildServiceProvider();
        var warmup = new ClientCertificateRevocationCheckerWarmup(serviceProvider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            warmup.StartAsync(CancellationToken.None)
        );

        Assert.Equal("No source is registered for 'denylist'.", exception.Message);
    }

    #endregion
}
