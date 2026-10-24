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

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault.Tests;

public class AzureKeyVaultKeyIdTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private const string HttpKeyId = "http://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string SecretId = "https://contoso.vault.azure.net/secrets/ncode-identity-root";

    private const string ShortVersionId = KeyId + "/0123456789abcdef0123456789abcde";

    #region IsKeyId Tests

    [Fact]
    public void IsKeyId_KeyId_ReturnsTrue()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(KeyId);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyId_KeyVersionId_ReturnsTrue()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(KeyVersionId);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyId_ManagedHsmKeyId_ReturnsTrue()
    {
        const string value = "https://contoso.managedhsm.azure.net/keys/root";

        var result = AzureKeyVaultKeyId.IsKeyId(value);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyId_HttpScheme_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(HttpKeyId);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyId_SecretId_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(SecretId);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyId_ShortVersion_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(ShortVersionId);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyId_TrailingSlash_ReturnsFalse()
    {
        const string value = KeyId + "/";

        var result = AzureKeyVaultKeyId.IsKeyId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyId_TrailingNewline_ReturnsFalse()
    {
        const string value = KeyId + "\n";

        var result = AzureKeyVaultKeyId.IsKeyId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyId_Null_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyId(null);

        Assert.False(result);
    }

    #endregion

    #region IsKeyVersionId Tests

    [Fact]
    public void IsKeyVersionId_KeyVersionId_ReturnsTrue()
    {
        var result = AzureKeyVaultKeyId.IsKeyVersionId(KeyVersionId);

        Assert.True(result);
    }

    [Fact]
    public void IsKeyVersionId_KeyId_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyVersionId(KeyId);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_HttpScheme_ReturnsFalse()
    {
        const string value = HttpKeyId + "/0123456789abcdef0123456789abcdef";

        var result = AzureKeyVaultKeyId.IsKeyVersionId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_SecretId_ReturnsFalse()
    {
        const string value = SecretId + "/0123456789abcdef0123456789abcdef";

        var result = AzureKeyVaultKeyId.IsKeyVersionId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_ShortVersion_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyVersionId(ShortVersionId);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_TrailingSlash_ReturnsFalse()
    {
        const string value = KeyVersionId + "/";

        var result = AzureKeyVaultKeyId.IsKeyVersionId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_TrailingNewline_ReturnsFalse()
    {
        const string value = KeyVersionId + "\n";

        var result = AzureKeyVaultKeyId.IsKeyVersionId(value);

        Assert.False(result);
    }

    [Fact]
    public void IsKeyVersionId_Null_ReturnsFalse()
    {
        var result = AzureKeyVaultKeyId.IsKeyVersionId(null);

        Assert.False(result);
    }

    #endregion
}
