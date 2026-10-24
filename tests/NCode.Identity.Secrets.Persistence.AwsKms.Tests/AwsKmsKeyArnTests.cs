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

using Xunit;

namespace NCode.Identity.Secrets.Persistence.AwsKms.Tests;

public class AwsKmsKeyArnTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private const string AliasArn = "arn:aws:kms:us-west-2:111122223333:alias/ncode-identity-root";

    #region IsValid Tests

    [Fact]
    public void IsValid_KeyArn_ReturnsTrue()
    {
        var result = AwsKmsKeyArn.IsValid(KeyArn);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_AliasArn_ReturnsTrue()
    {
        var result = AwsKmsKeyArn.IsValid(AliasArn);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_UsGovPartition_ReturnsTrue()
    {
        const string value =
            "arn:aws-us-gov:kms:us-gov-west-1:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

        var result = AwsKmsKeyArn.IsValid(value);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_WrongService_ReturnsFalse()
    {
        const string value =
            "arn:aws:iam:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

        var result = AwsKmsKeyArn.IsValid(value);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ShortAccount_ReturnsFalse()
    {
        const string value =
            "arn:aws:kms:us-west-2:11112222333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

        var result = AwsKmsKeyArn.IsValid(value);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_NonAsciiDigitAccount_ReturnsFalse()
    {
        // Twelve Arabic-Indic digits, which a Unicode-aware digit class would accept as an account number.
        const string account =
            "\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669\u0660\u0661";
        var value = $"arn:aws:kms:us-west-2:{account}:key/1234abcd-12ab-34cd-56ef-1234567890ab";

        var result = AwsKmsKeyArn.IsValid(value);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        var result = AwsKmsKeyArn.IsValid(null);

        Assert.False(result);
    }

    #endregion

    #region GetRegion Tests

    [Fact]
    public void GetRegion_KeyArn_ReturnsRegion()
    {
        var region = AwsKmsKeyArn.GetRegion(KeyArn);

        Assert.Equal("us-west-2", region);
    }

    [Fact]
    public void GetRegion_UsGovAliasArn_ReturnsRegion()
    {
        const string value =
            "arn:aws-us-gov:kms:us-gov-west-1:111122223333:alias/ncode-identity-root";

        var region = AwsKmsKeyArn.GetRegion(value);

        Assert.Equal("us-gov-west-1", region);
    }

    #endregion
}
