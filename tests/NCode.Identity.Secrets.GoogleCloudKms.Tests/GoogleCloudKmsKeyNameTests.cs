// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class GoogleCloudKmsKeyNameTests
{
    private const string KeyRingName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity";

    private const string CryptoKeyName = KeyRingName + "/cryptoKeys/root";

    private const string CryptoKeyVersionName = CryptoKeyName + "/cryptoKeyVersions/1";

    #region IsKeyRingName Tests

    [Fact]
    public void IsKeyRingName_KeyRingName_ReturnsTrue()
    {
        var result = GoogleCloudKmsKeyName.IsKeyRingName(KeyRingName);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyRingName_DomainScopedProject_ReturnsTrue()
    {
        const string value =
            "projects/example.com:my-project/locations/us-central1/keyRings/ncode-identity";

        var result = GoogleCloudKmsKeyName.IsKeyRingName(value);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyRingName_CryptoKeyName_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsKeyRingName(CryptoKeyName);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyRingName_LowerCaseKeyRings_ReturnsFalse()
    {
        const string value = "projects/my-project/locations/us-central1/keyrings/ncode-identity";

        var result = GoogleCloudKmsKeyName.IsKeyRingName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyRingName_KeyRingIdOver63Characters_ReturnsFalse()
    {
        var value = "projects/my-project/locations/us-central1/keyRings/" + new string('a', 64);

        var result = GoogleCloudKmsKeyName.IsKeyRingName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyRingName_TrailingNewline_ReturnsFalse()
    {
        const string value = KeyRingName + "\n";

        var result = GoogleCloudKmsKeyName.IsKeyRingName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyRingName_Null_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsKeyRingName(null);

        Assert.False(result);
    }

    #endregion

    #region IsCryptoKeyName Tests

    [Fact]
    public void IsCryptoKeyName_CryptoKeyName_ReturnsTrue()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(CryptoKeyName);

        Assert.True(result);
    }

    [Fact]
    public void IsCryptoKeyName_DomainScopedProject_ReturnsTrue()
    {
        const string value =
            "projects/example.com:my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(value);

        Assert.True(result);
    }

    [Fact]
    public void IsCryptoKeyName_CryptoKeyVersionName_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(CryptoKeyVersionName);

        Assert.False(result);
    }

    [Fact]
    public void IsCryptoKeyName_LowerCaseKeyRings_ReturnsFalse()
    {
        const string value =
            "projects/my-project/locations/us-central1/keyrings/ncode-identity/cryptoKeys/root";

        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsCryptoKeyName_TrailingNewline_ReturnsFalse()
    {
        const string value = CryptoKeyName + "\n";

        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsCryptoKeyName_Null_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyName(null);

        Assert.False(result);
    }

    #endregion

    #region IsCryptoKeyVersionName Tests

    [Fact]
    public void IsCryptoKeyVersionName_CryptoKeyVersionName_ReturnsTrue()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyVersionName(CryptoKeyVersionName);

        Assert.True(result);
    }

    [Fact]
    public void IsCryptoKeyVersionName_CryptoKeyName_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyVersionName(CryptoKeyName);

        Assert.False(result);
    }

    [Fact]
    public void IsCryptoKeyVersionName_TrailingNewline_ReturnsFalse()
    {
        const string value = CryptoKeyVersionName + "\n";

        var result = GoogleCloudKmsKeyName.IsCryptoKeyVersionName(value);

        Assert.False(result);
    }

    [Fact]
    public void IsCryptoKeyVersionName_Null_ReturnsFalse()
    {
        var result = GoogleCloudKmsKeyName.IsCryptoKeyVersionName(null);

        Assert.False(result);
    }

    #endregion

    #region GetCryptoKeyName Tests

    [Fact]
    public void GetCryptoKeyName_CryptoKeyVersionName_ReturnsCryptoKeyName()
    {
        var result = GoogleCloudKmsKeyName.GetCryptoKeyName(CryptoKeyVersionName);

        Assert.Equal(CryptoKeyName, result);
    }

    #endregion
}
