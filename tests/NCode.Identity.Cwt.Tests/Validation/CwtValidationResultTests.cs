// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cwt.Validation;

namespace NCode.Identity.Cwt.Tests.Validation;

public class CwtValidationResultTests
{
    #region Fail Tests

    [Fact]
    public void Fail_EmptyReason_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CwtValidationResult.Fail(CwtValidationFailureCodes.InvalidToken, "")
        );
    }

    [Fact]
    public void Fail_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CwtValidationResult.Fail("", "The token is refused.")
        );
    }

    [Fact]
    public void Fail_CodeAndReason_IsNotValid()
    {
        var result = CwtValidationResult.Fail(
            CwtValidationFailureCodes.InvalidToken,
            "The token is refused."
        );

        Assert.False(result.IsValid);
        Assert.Equal(CwtValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal("The token is refused.", result.FailureReason);
    }

    #endregion
}
