// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests;

public class GoogleServiceAccountEmailTests
{
    private const string Email = "ncode-identity@tenant-project.iam.gserviceaccount.com";

    #region IsValid Tests

    [Fact]
    public void IsValid_ServiceAccountEmail_ReturnsTrue()
    {
        var result = GoogleServiceAccountEmail.IsValid(Email);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_DomainScopedProject_ReturnsTrue()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            "tenant-root@example.com.my-project.iam.gserviceaccount.com"
        );

        Assert.True(result);
    }

    [Fact]
    public void IsValid_DefaultComputeServiceAccount_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            "123456789012-compute@developer.gserviceaccount.com"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_UserEmail_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid("someone@example.com");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_AccountIdTooShort_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            "abcd@tenant-project.iam.gserviceaccount.com"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_AccountIdTooLong_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            new string('a', 31) + "@tenant-project.iam.gserviceaccount.com"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_AccountIdStartsWithDigit_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            "1ncode-identity@tenant-project.iam.gserviceaccount.com"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_NonAsciiAccountId_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(
            "ncode-identit\u00FF@tenant-project.iam.gserviceaccount.com"
        );

        Assert.False(result);
    }

    [Fact]
    public void IsValid_TrailingNewline_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(Email + "\n");

        Assert.False(result);
    }

    [Fact]
    public void IsValid_Null_ReturnsFalse()
    {
        var result = GoogleServiceAccountEmail.IsValid(null);

        Assert.False(result);
    }

    #endregion
}
