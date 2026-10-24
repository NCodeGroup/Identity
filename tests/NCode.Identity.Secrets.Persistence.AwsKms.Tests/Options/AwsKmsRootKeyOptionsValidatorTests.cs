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

using NCode.Identity.Secrets.Persistence.AwsKms.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.Secrets.Persistence.AwsKms.Tests.Options;

public class AwsKmsRootKeyOptionsValidatorTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private static AwsKmsRootKeyOptionsValidator CreateValidator(string? rootKeyProvider) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = rootKeyProvider }
            )
        );

    #region Validate Tests

    [Fact]
    public void Validate_OtherProviderSelected_Skips()
    {
        var validator = CreateValidator(RootKeyProviderNames.Local);

        var result = validator.Validate(null, new AwsKmsRootKeyOptions { KeyArn = null });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_MissingKeyArn_Fails()
    {
        var validator = CreateValidator(RootKeyProviderNames.AwsKms);

        var result = validator.Validate(null, new AwsKmsRootKeyOptions { KeyArn = null });

        Assert.True(result.Failed);
        Assert.Contains("KeyArn", result.FailureMessage);
    }

    [Fact]
    public void Validate_MalformedKeyArn_Fails()
    {
        var validator = CreateValidator(RootKeyProviderNames.AwsKms);

        var result = validator.Validate(null, new AwsKmsRootKeyOptions { KeyArn = "not-an-arn" });

        Assert.True(result.Failed);
        Assert.Contains("KeyArn", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyArn_Succeeds()
    {
        var validator = CreateValidator(RootKeyProviderNames.AwsKms);

        var result = validator.Validate(null, new AwsKmsRootKeyOptions { KeyArn = KeyArn });

        Assert.True(result.Succeeded);
    }

    #endregion
}
