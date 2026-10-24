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

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Tests;

public class GoogleCloudKmsKeyNameTests
{
    private const string CryptoKeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private const string CryptoKeyVersionName = CryptoKeyName + "/cryptoKeyVersions/1";

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
