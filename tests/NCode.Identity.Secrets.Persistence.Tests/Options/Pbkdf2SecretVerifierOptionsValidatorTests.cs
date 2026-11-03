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
    public void Validate_WhenTheIterationsAreOne_Succeeds()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions { Iterations = 1 }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheIterationsAreBelowOne_ReportsThem()
    {
        var result = new Pbkdf2SecretVerifierOptionsValidator().Validate(
            null,
            new Pbkdf2SecretVerifierOptions { Iterations = 0 }
        );

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(
            "'Pbkdf2SecretVerifierOptions.Iterations' is 0; it must be at least 1.",
            failure
        );
    }

    #endregion
}
