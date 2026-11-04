// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Options;

namespace NCode.Identity.Jose.SdJwt.Tests.Issuance;

public class DefaultSdJwtRandomSourceTests
{
    private static DefaultSdJwtRandomSource CreateRandomSource(int saltSizeBytes) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SdJwtOptions { SaltSizeBytes = saltSizeBytes }
            )
        );

    #region NextSalt Tests

    [Theory]
    [InlineData(16, 22)]
    [InlineData(32, 43)]
    [InlineData(257, 343)]
    [InlineData(1024, 1366)]
    public void NextSalt_WithSaltSize_EncodesThatManyRandomBytes(int saltSizeBytes, int length)
    {
        var randomSource = CreateRandomSource(saltSizeBytes);

        var salt = randomSource.NextSalt();

        Assert.Equal(length, salt.Length);
        Assert.Equal(saltSizeBytes, Base64Url.DecodeFromChars(salt).Length);
    }

    [Fact]
    public void NextSalt_CalledTwice_ReturnsDifferentSalts()
    {
        var randomSource = CreateRandomSource(16);

        var first = randomSource.NextSalt();
        var second = randomSource.NextSalt();

        Assert.NotEqual(first, second);
    }

    #endregion

    #region NextIndex Tests

    [Fact]
    public void NextIndex_WithBoundOfOne_ReturnsZero()
    {
        var randomSource = CreateRandomSource(16);

        Assert.Equal(0, randomSource.NextIndex(1));
    }

    [Fact]
    public void NextIndex_WithBound_StaysBelowIt()
    {
        var randomSource = CreateRandomSource(16);

        var indexes = Enumerable.Range(0, 64).Select(_ => randomSource.NextIndex(4)).ToList();

        Assert.All(indexes, x => Assert.InRange(x, 0, 3));
    }

    #endregion
}
