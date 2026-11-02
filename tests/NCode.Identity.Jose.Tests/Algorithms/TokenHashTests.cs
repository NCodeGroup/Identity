// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    #region Span Overload Tests

    [Fact]
    public void Compute_WhenBufferSupplied_ReturnsLeftHalfBase64Url()
    {
        const string value = "access-token";
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var result = TokenHash.Compute(Sha256, value, hashBuffer);

        Assert.Equal(ExpectedHash(value), result);
    }

    [Fact]
    public void Compute_WhenBufferIsReused_ReturnsTheRightValueForEachCall()
    {
        const string first = "access-token";
        const string second = "authorization-code";
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var firstResult = TokenHash.Compute(Sha256, first, hashBuffer);
        var secondResult = TokenHash.Compute(Sha256, second, hashBuffer);

        Assert.Equal(ExpectedHash(first), firstResult);
        Assert.Equal(ExpectedHash(second), secondResult);
    }

    [Fact]
    public void Compute_WhenValueIsLongerThanTheStackLimit_ReturnsLeftHalfBase64Url()
    {
        var value = new string('a', 1000);
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var result = TokenHash.Compute(Sha256, value, hashBuffer);

        Assert.Equal(ExpectedHash(value), result);
    }

    [Fact]
    public void Compute_WhenBufferIsEmpty_Throws()
    {
        var hashBuffer = Array.Empty<byte>();

        Assert.Throws<ArgumentException>(() =>
            TokenHash.Compute(Sha256, "access-token", hashBuffer.AsSpan())
        );
    }

    [Fact]
    public void Compute_WhenHashFunctionFails_Throws()
    {
        var hashBuffer = new byte[Sha256SizeBytes];

        Assert.Throws<InvalidOperationException>(() =>
            TokenHash.Compute(FailingHash, "access-token", hashBuffer.AsSpan())
        );
    }

    [Fact]
    public void Compute_WhenHashFunctionWritesTheWrongSize_Throws()
    {
        var hashBuffer = new byte[Sha256SizeBytes];

        Assert.Throws<InvalidOperationException>(() =>
            TokenHash.Compute(WrongSizeHash, "access-token", hashBuffer.AsSpan())
        );
    }

    [Fact]
    public void Compute_WhenSpanIsNotAscii_Throws()
    {
        var hashBuffer = new byte[Sha256SizeBytes];

        Assert.Throws<ArgumentException>(() =>
            TokenHash.Compute(Sha256, "tökén", hashBuffer.AsSpan())
        );
    }

    [Fact]
    public void Verify_WhenBufferSupplied_ReturnsTrueForMatchAndFalseForDifference()
    {
        const string value = "access-token";
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var matches = TokenHash.Verify(Sha256, value, ExpectedHash(value), hashBuffer);
        var differs = TokenHash.Verify(Sha256, value, ExpectedHash("other"), hashBuffer);

        Assert.True(matches);
        Assert.False(differs);
    }

    [Fact]
    public void Verify_WhenTokenHashHasTheWrongLength_ReturnsFalse()
    {
        const string value = "access-token";
        var tokenHash = ExpectedHash(value)[..^1];
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var result = TokenHash.Verify(Sha256, value, tokenHash, hashBuffer);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WhenValueIsNotAscii_ReturnsFalse()
    {
        const string value = "tökén";
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var result = TokenHash.Verify(Sha256, value, ExpectedHash("access-token"), hashBuffer);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WhenTokenHashIsNotAsciiButHasTheRightLength_ReturnsFalse()
    {
        const string value = "access-token";
        var tokenHash = 'é' + ExpectedHash(value)[1..];
        Span<byte> hashBuffer = stackalloc byte[Sha256SizeBytes];

        var result = TokenHash.Verify(Sha256, value, tokenHash, hashBuffer);

        Assert.False(result);
    }

    private static bool FailingHash(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        out int bytesWritten
    )
    {
        bytesWritten = 0;
        return false;
    }

    private static bool WrongSizeHash(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        out int bytesWritten
    )
    {
        bytesWritten = destination.Length - 1;
        return true;
    }

    #endregion
}
