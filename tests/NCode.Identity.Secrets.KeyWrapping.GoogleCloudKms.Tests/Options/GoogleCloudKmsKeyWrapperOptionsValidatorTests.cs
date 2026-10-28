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

using NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence.Options;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests.Options;

public class GoogleCloudKmsKeyWrapperOptionsValidatorTests
{
    private const string KeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private const string KeyVersionName = KeyName + "/cryptoKeyVersions/1";

    private static GoogleCloudKmsKeyWrapperOptionsValidator CreateValidator(
        string? rootKeyProvider
    ) =>
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

        var result = validator.Validate(
            null,
            new GoogleCloudKmsKeyWrapperOptions { KeyName = null }
        );

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_MissingKeyName_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.GoogleCloudKms);

        var result = validator.Validate(
            null,
            new GoogleCloudKmsKeyWrapperOptions { KeyName = null }
        );

        Assert.True(result.Failed);
        Assert.Contains("KeyName", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyVersionName_Fails()
    {
        var validator = CreateValidator(KeyWrapperNames.GoogleCloudKms);

        var result = validator.Validate(
            null,
            new GoogleCloudKmsKeyWrapperOptions { KeyName = KeyVersionName }
        );

        Assert.True(result.Failed);
        Assert.Contains("KeyName", result.FailureMessage);
    }

    [Fact]
    public void Validate_KeyName_Succeeds()
    {
        var validator = CreateValidator(KeyWrapperNames.GoogleCloudKms);

        var result = validator.Validate(
            null,
            new GoogleCloudKmsKeyWrapperOptions { KeyName = KeyName }
        );

        Assert.True(result.Succeeded);
    }

    #endregion
}
