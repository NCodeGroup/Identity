// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Options;

public class SecretVerificationGateOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WhenTheOptionsAreTheDefaults_Succeeds()
    {
        var result = new SecretVerificationGateOptionsValidator().Validate(
            null,
            new SecretVerificationGateOptions()
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheOptionsAreAtTheirLeast_Succeeds()
    {
        var result = new SecretVerificationGateOptionsValidator().Validate(
            null,
            new SecretVerificationGateOptions
            {
                MaxConcurrency = 1,
                MaxQueueLength = 0,
                QueueTimeout = TimeSpan.FromMilliseconds(1),
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheOptionsAreOutOfRange_ReportsEachOne()
    {
        var result = new SecretVerificationGateOptionsValidator().Validate(
            null,
            new SecretVerificationGateOptions
            {
                MaxConcurrency = -1,
                MaxQueueLength = -1,
                QueueTimeout = TimeSpan.Zero,
            }
        );

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Collection(
            result.Failures,
            failure =>
                Assert.Equal(
                    "'SecretVerificationGateOptions.MaxConcurrency' is -1; it must be at least 1.",
                    failure
                ),
            failure =>
                Assert.Equal(
                    "'SecretVerificationGateOptions.MaxQueueLength' is -1; it must be at least 0.",
                    failure
                ),
            failure =>
                Assert.Equal(
                    "'SecretVerificationGateOptions.QueueTimeout' is 00:00:00; it must be from 1 millisecond to "
                        + "2147483647 milliseconds.",
                    failure
                )
        );
    }

    [Fact]
    public void Validate_WhenTheQueueTimeoutIsBeyondATimersRange_ReportsIt()
    {
        var result = new SecretVerificationGateOptionsValidator().Validate(
            null,
            new SecretVerificationGateOptions
            {
                QueueTimeout =
                    TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1),
            }
        );

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            "'SecretVerificationGateOptions.QueueTimeout' is ",
            failure,
            StringComparison.Ordinal
        );
    }

    #endregion
}
