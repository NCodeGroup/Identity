// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests;

public class GoogleCloudKmsCallSettingsTests
{
    private static bool Retries(Predicate<Exception> filter, StatusCode statusCode) =>
        filter(new RpcException(new Status(statusCode, "failed")));

    #region Retrying Tests

    [Fact]
    public void Retrying_Settings_HasRetry()
    {
        var retry = GoogleCloudKmsCallSettings.Retrying.Retry;

        Assert.NotNull(retry);
    }

    [Fact]
    public void Retrying_Settings_UsesExponentialBackoff()
    {
        var retry = GoogleCloudKmsCallSettings.Retrying.Retry;

        Assert.NotNull(retry);
        Assert.Equal(4, retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), retry.InitialBackoff);
        Assert.Equal(TimeSpan.FromSeconds(4), retry.MaxBackoff);
        Assert.Equal(2.0, retry.BackoffMultiplier);
    }

    [Theory]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public void Retrying_TransientStatus_Retries(StatusCode statusCode)
    {
        var retry = GoogleCloudKmsCallSettings.Retrying.Retry;

        Assert.NotNull(retry);
        Assert.True(Retries(retry.RetryFilter, statusCode));
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.AlreadyExists)]
    [InlineData(StatusCode.FailedPrecondition)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.InvalidArgument)]
    public void Retrying_OtherStatus_DoesNotRetry(StatusCode statusCode)
    {
        var retry = GoogleCloudKmsCallSettings.Retrying.Retry;

        Assert.NotNull(retry);
        Assert.False(Retries(retry.RetryFilter, statusCode));
    }

    [Fact]
    public void Retrying_NonRpcException_DoesNotRetry()
    {
        var retry = GoogleCloudKmsCallSettings.Retrying.Retry;

        Assert.NotNull(retry);
        Assert.False(retry.RetryFilter(new InvalidOperationException()));
    }

    #endregion

    #region Creating Tests

    [Fact]
    public void Creating_Settings_UsesExponentialBackoff()
    {
        var retry = GoogleCloudKmsCallSettings.Creating.Retry;

        Assert.NotNull(retry);
        Assert.Equal(3, retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), retry.InitialBackoff);
        Assert.Equal(TimeSpan.FromSeconds(4), retry.MaxBackoff);
        Assert.Equal(2.0, retry.BackoffMultiplier);
    }

    [Fact]
    public void Creating_ResourceExhausted_Retries()
    {
        var retry = GoogleCloudKmsCallSettings.Creating.Retry;

        Assert.NotNull(retry);
        Assert.True(Retries(retry.RetryFilter, StatusCode.ResourceExhausted));
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.AlreadyExists)]
    public void Creating_OtherStatus_DoesNotRetry(StatusCode statusCode)
    {
        var retry = GoogleCloudKmsCallSettings.Creating.Retry;

        Assert.NotNull(retry);
        Assert.False(Retries(retry.RetryFilter, statusCode));
    }

    #endregion
}
