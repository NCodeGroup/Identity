// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Options;

public class Pbkdf2SecretVerifierOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WhenTheOptionsAreTheDefaults_Succeeds()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions()
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheOptionsAreOne_Succeeds()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions
            {
                Iterations = 1,
                MaxConcurrency = 1,
                MaxQueueLength = 0,
                QueueTimeout = TimeSpan.FromMilliseconds(1),
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheOptionsAreBelowOne_ReportsEachOne()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions
            {
                Iterations = 0,
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
                    "'Pbkdf2SecretVerifierOptions.Iterations' is 0; it must be at least 1.",
                    failure
                ),
            failure =>
                Assert.Equal(
                    "'Pbkdf2SecretVerifierOptions.MaxConcurrency' is -1; it must be at least 1.",
                    failure
                ),
            failure =>
                Assert.Equal(
                    "'Pbkdf2SecretVerifierOptions.MaxQueueLength' is -1; it must be at least 0.",
                    failure
                ),
            failure =>
                Assert.Equal(
                    "'Pbkdf2SecretVerifierOptions.QueueTimeout' is 00:00:00; it must be from 1 millisecond to "
                        + "2147483647 milliseconds.",
                    failure
                )
        );
    }

    [Fact]
    public void Validate_WhenTheQueueTimeoutIsBeyondATimersRange_ReportsIt()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions
            {
                QueueTimeout =
                    TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1),
            }
        );

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            "'Pbkdf2SecretVerifierOptions.QueueTimeout' is ",
            failure,
            StringComparison.Ordinal
        );
    }

    #endregion
}
