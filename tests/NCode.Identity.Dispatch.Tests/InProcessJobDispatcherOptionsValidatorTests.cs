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
            "InProcessJobDispatcherOptions.MaxConcurrentJobs",
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
            "InProcessJobDispatcherOptions.QueueCapacity",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
