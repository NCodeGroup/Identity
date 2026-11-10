// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Amazon.KeyManagementService;
using NCode.Identity.Secrets.AwsKms.Facades;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests.Facades;

public class DefaultAwsKmsClientFactoryTests
{
    private const string Region = "us-west-2";

    private const string OtherRegion = "eu-central-1";

    private static DefaultAwsKmsClientFactory CreateFactory() => new(TimeProvider.System);

    // The client that the operation receives, which the factory hands out for the region.
    private static async ValueTask<IAmazonKeyManagementService> GetClientAsync(
        DefaultAwsKmsClientFactory factory,
        string region,
        CancellationToken cancellationToken
    ) =>
        await factory.ExecuteAsync(
            region,
            access: null,
            (client, _) => ValueTask.FromResult(client),
            cancellationToken
        );

    // The client that the operation receives, which the factory hands out for a call that is never retried.
    private static async ValueTask<IAmazonKeyManagementService> GetClientWithoutRetryAsync(
        DefaultAwsKmsClientFactory factory,
        string region,
        CancellationToken cancellationToken
    ) =>
        await factory.ExecuteWithoutRetryAsync(
            region,
            (client, _) => ValueTask.FromResult(client),
            cancellationToken
        );

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_SameRegion_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(factory, Region, CancellationToken.None);
        var second = await GetClientAsync(factory, Region, CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ExecuteAsync_Region_RunsOperationsWithTheRetryingConfiguration()
    {
        using var factory = CreateFactory();

        var client = await GetClientAsync(factory, Region, CancellationToken.None);

        Assert.Equal(AwsKmsClientConfiguration.MaxErrorRetry, client.Config.MaxErrorRetry);
        Assert.Equal(Region, client.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.ExecuteAsync<int>(Region, access: null, null!, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region ExecuteWithoutRetryAsync Tests

    [Fact]
    public async Task ExecuteWithoutRetryAsync_Region_RunsOperationsWithAClientThatNeverRetries()
    {
        using var factory = CreateFactory();

        var client = await GetClientWithoutRetryAsync(factory, Region, CancellationToken.None);

        Assert.Equal(0, client.Config.MaxErrorRetry);
        Assert.Equal(Region, client.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public async Task ExecuteWithoutRetryAsync_SameRegion_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetClientWithoutRetryAsync(factory, Region, CancellationToken.None);
        var second = await GetClientWithoutRetryAsync(factory, Region, CancellationToken.None);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ExecuteWithoutRetryAsync_DifferentRegions_RunsOperationsWithDifferentClients()
    {
        using var factory = CreateFactory();

        var first = await GetClientWithoutRetryAsync(factory, Region, CancellationToken.None);
        var second = await GetClientWithoutRetryAsync(factory, OtherRegion, CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(OtherRegion, second.Config.RegionEndpoint.SystemName);
    }

    [Fact]
    public async Task ExecuteWithoutRetryAsync_SameRegion_RunsOperationsWithAnotherClientThanTheRetryingOne()
    {
        using var factory = CreateFactory();

        var retrying = await GetClientAsync(factory, Region, CancellationToken.None);
        var withoutRetry = await GetClientWithoutRetryAsync(
            factory,
            Region,
            CancellationToken.None
        );

        Assert.NotSame(retrying, withoutRetry);
    }

    [Fact]
    public async Task ExecuteWithoutRetryAsync_Operation_ReturnsItsResult()
    {
        using var factory = CreateFactory();

        var result = await factory.ExecuteWithoutRetryAsync(
            Region,
            (_, _) => ValueTask.FromResult(42),
            CancellationToken.None
        );

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteWithoutRetryAsync_NullOperation_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.ExecuteWithoutRetryAsync<int>(Region, null!, CancellationToken.None).AsTask()
        );
    }

    #endregion
}
