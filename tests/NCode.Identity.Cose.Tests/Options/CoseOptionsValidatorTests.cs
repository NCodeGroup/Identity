// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Options;

namespace NCode.Identity.Cose.Tests.Options;

public class CoseOptionsValidatorTests
{
    private static readonly CoseOptionsValidator Validator = new();

    #region Validate

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        Assert.True(Validator.Validate(null, new CoseOptions()).Succeeded);
    }

    [Theory]
    [InlineData(nameof(CoseOptions.MaxMessageSizeBytes))]
    [InlineData(nameof(CoseOptions.MaxNestingDepth))]
    [InlineData(nameof(CoseOptions.MaxSignatures))]
    [InlineData(nameof(CoseOptions.MaxRecipients))]
    public void Validate_LimitBelowOne_FailsNamingTheProperty(string property)
    {
        var options = new CoseOptions();
        typeof(CoseOptions).GetProperty(property)!.SetValue(options, 0);

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(property, string.Join(' ', result.Failures!));
    }

    #endregion
}
