// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using NCode.Identity.Dispatch.InProcess;
using NCode.Identity.Dispatch.Options;
using Xunit;

namespace NCode.Identity.Dispatch;

public class InProcessJobDispatcherOptionsValidatorTests
{
    private InProcessJobDispatcherOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WithTheDefaults_Succeeds()
    {
        var result = Validator.Validate(null, new InProcessJobDispatcherOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenMaxConcurrentJobsIsBelowOne_Fails(int maxConcurrentJobs)
    {
        var options = new InProcessJobDispatcherOptions { MaxConcurrentJobs = maxConcurrentJobs };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "InProcessJobDispatcherOptions.MaxConcurrentJobs ('InProcessJobDispatcher:MaxConcurrentJobs')",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenQueueCapacityIsBelowOne_Fails(int queueCapacity)
    {
        var options = new InProcessJobDispatcherOptions { QueueCapacity = queueCapacity };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "InProcessJobDispatcherOptions.QueueCapacity ('InProcessJobDispatcher:QueueCapacity')",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenFullModeIsNotAMember_Fails()
    {
        var options = new InProcessJobDispatcherOptions { FullMode = (BoundedChannelFullMode)7 };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "InProcessJobDispatcherOptions.FullMode ('InProcessJobDispatcher:FullMode')",
            result.FailureMessage,
            StringComparison.Ordinal
        );
        Assert.Contains("it is 7.", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BoundedChannelFullMode.Wait)]
    [InlineData(BoundedChannelFullMode.DropNewest)]
    [InlineData(BoundedChannelFullMode.DropOldest)]
    [InlineData(BoundedChannelFullMode.DropWrite)]
    public void Validate_WhenFullModeIsAMember_Succeeds(BoundedChannelFullMode fullMode)
    {
        var options = new InProcessJobDispatcherOptions { FullMode = fullMode };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    #endregion
}
