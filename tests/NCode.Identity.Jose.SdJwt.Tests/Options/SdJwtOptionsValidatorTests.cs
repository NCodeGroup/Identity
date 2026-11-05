// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.SdJwt.Options;

namespace NCode.Identity.Jose.SdJwt.Tests.Options;

public class SdJwtOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        var validator = new SdJwtOptionsValidator();

        var result = validator.Validate(null, new SdJwtOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenSaltIsTooShort_Fails()
    {
        var validator = new SdJwtOptionsValidator();

        var result = validator.Validate(null, new SdJwtOptions { SaltSizeBytes = 15 });

        Assert.True(result.Failed);
        Assert.Equal(
            "SdJwtOptions.SaltSizeBytes must be at least 16; it is 15.",
            result.FailureMessage
        );
    }

    [Fact]
    public void Validate_WhenLimitsAreBelowOne_ReportsEveryFailure()
    {
        var validator = new SdJwtOptionsValidator();

        var result = validator.Validate(
            null,
            new SdJwtOptions { MaxDisclosureCount = 0, MaxNestingDepth = 0 }
        );

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Equal(2, result.Failures.Count());
    }

    #endregion
}
