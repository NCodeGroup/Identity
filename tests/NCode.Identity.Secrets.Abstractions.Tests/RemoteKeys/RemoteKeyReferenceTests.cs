// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyReferenceTests
{
    private const string ArnKeyId =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    #region Constructor Tests

    [Fact]
    public void Constructor_Valid_SetsProperties()
    {
        var reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, ArnKeyId);

        Assert.Equal(RemoteKeyProviderNames.AwsKms, reference.Provider);
        Assert.Equal(ArnKeyId, reference.KeyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AWS")]
    [InlineData("aws_kms")]
    [InlineData("-aws")]
    [InlineData("aws-")]
    [InlineData("aws--kms")]
    [InlineData("aws:kms")]
    [InlineData("aws\n")]
    public void Constructor_InvalidProvider_ThrowsArgumentException(string provider)
    {
        Assert.Throws<ArgumentException>(() => new RemoteKeyReference(provider, "key"));
    }

    [Fact]
    public void Constructor_NullProvider_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RemoteKeyReference(null!, "key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Constructor_EmptyKeyId_ThrowsArgumentException(string? keyId)
    {
        Assert.Throws<ArgumentException>(() =>
            new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, keyId!)
        );
    }

    #endregion

    #region ToString and TryParse Tests

    [Fact]
    public void ToString_Reference_ReturnsProviderColonKeyId()
    {
        var reference = new RemoteKeyReference(
            RemoteKeyProviderNames.AzureKeyVault,
            "https://vault/keys/k/v1"
        );

        Assert.Equal("azure-key-vault:https://vault/keys/k/v1", reference.ToString());
    }

    [Fact]
    public void TryParse_RoundTripWithArnKeyId_ReturnsEqualReference()
    {
        var expected = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, ArnKeyId);

        var result = RemoteKeyReference.TryParse(expected.ToString(), out var actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
        Assert.Equal(ArnKeyId, actual.KeyId);
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse()
    {
        var result = RemoteKeyReference.TryParse(null, out var reference);

        Assert.False(result);
        Assert.Equal(default, reference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("aws-kms")]
    [InlineData("AWS-KMS:key")]
    [InlineData(":key")]
    [InlineData("aws kms:key")]
    [InlineData("aws-kms:")]
    public void TryParse_Invalid_ReturnsFalse(string value)
    {
        var result = RemoteKeyReference.TryParse(value, out var reference);

        Assert.False(result);
        Assert.Equal(default, reference);
    }

    #endregion

    #region IsValidProviderName Tests

    [Theory]
    [InlineData(RemoteKeyProviderNames.AwsKms)]
    [InlineData(RemoteKeyProviderNames.AzureKeyVault)]
    [InlineData(RemoteKeyProviderNames.GoogleCloudKms)]
    [InlineData("vault2")]
    public void IsValidProviderName_Valid_ReturnsTrue(string name)
    {
        Assert.True(RemoteKeyReference.IsValidProviderName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Aws")]
    [InlineData("aws-")]
    [InlineData("aws\n")]
    public void IsValidProviderName_Invalid_ReturnsFalse(string? name)
    {
        Assert.False(RemoteKeyReference.IsValidProviderName(name));
    }

    #endregion
}
