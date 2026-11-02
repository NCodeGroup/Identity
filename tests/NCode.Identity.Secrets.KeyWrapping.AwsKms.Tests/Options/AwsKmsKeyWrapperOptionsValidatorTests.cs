// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;
using NCode.Identity.Secrets.Persistence.Options;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests.Options;

public class AwsKmsKeyWrapperOptionsValidatorTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private static AwsKmsKeyWrapperOptionsValidator CreateValidator(string? rootKeyProvider) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
            )
        );

    #region Validate Tests

    [Fact]
    public void Validate_OtherProviderSelected_Skips()
    {
        var validator = CreateValidator(KeyWrapperNames.Local);

        var result = validator.Validate(null, new AwsKmsKeyWrapperOptions { KeyArn = null });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_MissingKeyArn_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AwsKms);

        var result = validator.Validate(null, new AwsKmsKeyWrapperOptions { KeyArn = null });

        Assert.True(result.Failed);
        Assert.Contains("KeyArn", result.FailureMessage);
    }

    [Fact]
    public void Validate_MalformedKeyArn_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.AwsKms);

        var result = validator.Validate(
            null,
            new AwsKmsKeyWrapperOptions { KeyArn = "not-an-arn" }
        );

        Assert.True(result.Failed);
        Assert.Contains("KeyArn", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyArn_Succeeds()
    {
        var validator = CreateValidator(KeyWrapperNames.AwsKms);

        var result = validator.Validate(null, new AwsKmsKeyWrapperOptions { KeyArn = KeyArn });

        Assert.True(result.Succeeded);
    }

    #endregion
}
