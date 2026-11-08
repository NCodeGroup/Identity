// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests;

public class AwsIamRoleArnTests
{
    private const string RoleArn = "arn:aws:iam::111122223333:role/ncode-identity-tenant-root";

    #region IsValid Tests

    [Fact]
    public void IsValid_RoleArn_ReturnsTrue()
    {
        var result = AwsIamRoleArn.IsValid(RoleArn);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_RoleArnWithPath_ReturnsTrue()
    {
        var result = AwsIamRoleArn.IsValid(
            "arn:aws:iam::111122223333:role/division/team+a=b,c.d@e-f_g"
        );

        Assert.True(result);
    }

    [Fact]
    public void IsValid_UsGovPartition_ReturnsTrue()
    {
        var result = AwsIamRoleArn.IsValid("arn:aws-us-gov:iam::111122223333:role/tenant-root");

        Assert.True(result);
    }

    [Fact]
    public void IsValid_User_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid("arn:aws:iam::111122223333:user/tenant-root");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_KmsKeyArn_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid(
            "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ShortAccount_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid("arn:aws:iam::11112222333:role/tenant-root");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_NonAsciiDigitAccount_ReturnsFalse()
    {
        // Twelve Arabic-Indic digits, which a Unicode-aware digit class would accept as an account number.
        const string account =
            "\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669\u0660\u0661";

        var result = AwsIamRoleArn.IsValid($"arn:aws:iam::{account}:role/tenant-root");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_TrailingNewline_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid(RoleArn + "\n");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_RoleNameTooLong_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid(
            "arn:aws:iam::111122223333:role/" + new string('a', 577)
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        var result = AwsIamRoleArn.IsValid(null);

        Assert.False(result);
    }

    #endregion
}
