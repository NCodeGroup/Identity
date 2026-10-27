#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class TokenHashTests : BaseTests
{
    private const int Sha256SizeBytes = 32;

    private static HashFunctionDelegate Sha256 { get; } = SHA256.TryHashData;

    private static string ExpectedHash(string value) =>
        Base64Url.EncodeToString(
            SHA256.HashData(SecureEncoding.ASCII.GetBytes(value)).AsSpan(0, 16)
        );

    #region Compute Tests

    [Fact]
    public void Compute_WhenSha256_ReturnsLeftHalfBase64Url()
    {
        const string value = "access-token";

        var result = TokenHash.Compute(Sha256, Sha256SizeBytes, value);

        Assert.Equal(ExpectedHash(value), result);
    }

    [Fact]
    public void Compute_WhenNonAscii_Throws()
    {
        const string value = "tökén";

        Assert.Throws<ArgumentException>(() => TokenHash.Compute(Sha256, Sha256SizeBytes, value));
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WhenMatches_ReturnsTrue()
    {
        const string value = "access-token";
        var tokenHash = ExpectedHash(value);

        var result = TokenHash.Verify(Sha256, Sha256SizeBytes, value, tokenHash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WhenDiffers_ReturnsFalse()
    {
        const string value = "access-token";
        var tokenHash = ExpectedHash("other");

        var result = TokenHash.Verify(Sha256, Sha256SizeBytes, value, tokenHash);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WhenTokenHashNonAscii_ReturnsFalse()
    {
        const string value = "access-token";
        const string tokenHash = "tökén";

        var result = TokenHash.Verify(Sha256, Sha256SizeBytes, value, tokenHash);

        Assert.False(result);
    }

    #endregion
}
