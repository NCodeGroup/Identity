// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public class SecretVerifierFormatTests
{
    private static readonly KeyMetadata Metadata = new()
    {
        KeyId = "secret-1",
        ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
    };

    private static readonly byte[] Secret = SecureEncoding.UTF8.GetBytes("the-client-secret");
    private static readonly byte[] OtherSecret = SecureEncoding.UTF8.GetBytes("the-client-secreT");

    private static string Sha256Text(byte[] secret) =>
        "$ncode-sha256$" + Base64Url.EncodeToString(SHA256.HashData(secret));

    private static string Pbkdf2Text(byte[] secret, int iterations, int saltLength, int keyLength)
    {
        var salt = RandomNumberGenerator.GetBytes(saltLength);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            keyLength
        );
        return $"$ncode-pbkdf2-sha256${iterations}${Base64Url.EncodeToString(salt)}${Base64Url.EncodeToString(key)}";
    }

    private static SecretVerifier Parse(string text)
    {
        Assert.True(
            SecretVerifierFormat.TryParse(
                Metadata,
                SecureEncoding.ASCII.GetBytes(text),
                out var verifier
            )
        );
        return verifier;
    }

    private static void AssertRefused(string text)
    {
        Assert.False(
            SecretVerifierFormat.TryParse(
                Metadata,
                SecureEncoding.ASCII.GetBytes(text),
                out var verifier
            )
        );
        Assert.Null(verifier);
    }

    #region CreateSha256 Tests

    [Fact]
    public void CreateSha256_WritesTheSchemeAndTheDigest()
    {
        using var lease = SecretVerifierFormat.CreateSha256(Secret, out var verifier);

        Assert.Equal(Sha256Text(Secret), SecureEncoding.ASCII.GetString(verifier.Span));
    }

    [Fact]
    public void CreateSha256_RoundTripsThroughTryParse()
    {
        using var lease = SecretVerifierFormat.CreateSha256(Secret, out var text);

        Assert.True(SecretVerifierFormat.TryParse(Metadata, text.Span, out var verifier));
        Assert.Equal(SecretVerifierSchemes.Sha256, verifier.Scheme);
        Assert.Equal("secret-1", verifier.KeyId);
        Assert.Equal(Metadata, verifier.Metadata);
        Assert.True(verifier.Verify(Secret));
        Assert.False(verifier.Verify(OtherSecret));
        Assert.False(verifier.Verify([]));
    }

    #endregion

    #region CreatePbkdf2 Tests

    [Fact]
    public void CreatePbkdf2_WritesTheSchemeTheIterationsTheSaltAndTheDerivedKey()
    {
        using var lease = SecretVerifierFormat.CreatePbkdf2(Secret, 1_000, out var verifier);

        var segments = SecureEncoding.ASCII.GetString(verifier.Span).Split('$');
        Assert.Equal(5, segments.Length);
        Assert.Empty(segments[0]);
        Assert.Equal("ncode-pbkdf2-sha256", segments[1]);
        Assert.Equal("1000", segments[2]);

        var salt = Base64Url.DecodeFromChars(segments[3]);
        Assert.Equal(SecretVerifierFormat.Pbkdf2SaltByteCount, salt.Length);
        var expectedKey = Rfc2898DeriveBytes.Pbkdf2(
            Secret,
            salt,
            1_000,
            HashAlgorithmName.SHA256,
            SecretVerifierFormat.Pbkdf2DerivedKeyByteCount
        );
        Assert.Equal(expectedKey, Base64Url.DecodeFromChars(segments[4]));
    }

    [Fact]
    public void CreatePbkdf2_DrawsAFreshSaltEachTime()
    {
        using var first = SecretVerifierFormat.CreatePbkdf2(Secret, 1_000, out var firstText);
        using var second = SecretVerifierFormat.CreatePbkdf2(Secret, 1_000, out var secondText);

        Assert.False(firstText.Span.SequenceEqual(secondText.Span));
    }

    [Fact]
    public void CreatePbkdf2_RoundTripsThroughTryParse()
    {
        using var lease = SecretVerifierFormat.CreatePbkdf2(Secret, 1_000, out var text);

        Assert.True(SecretVerifierFormat.TryParse(Metadata, text.Span, out var verifier));
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.True(verifier.Verify(Secret));
        Assert.False(verifier.Verify(OtherSecret));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreatePbkdf2_WhenIterationsAreBelowOne_Throws(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var lease = SecretVerifierFormat.CreatePbkdf2(Secret, iterations, out _);
        });
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_ReadsAPbkdf2VerifierOfTheFamilysPasswordHashFormatWithOtherParameters()
    {
        // The parameters come from the text, so a verifier made with other parameters keeps verifying.
        var verifier = Parse(Pbkdf2Text(Secret, iterations: 1_234, saltLength: 32, keyLength: 64));

        Assert.True(verifier.Verify(Secret));
        Assert.False(verifier.Verify(OtherSecret));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ncode-sha256$AAAA")]
    [InlineData("$ncode-sha256")]
    [InlineData("$unknown-scheme$AAAA")]
    [InlineData("$NCODE-SHA256$47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU")]
    [InlineData("$ncode-sha256$")]
    [InlineData("$ncode-sha256$not base64url!")]
    [InlineData("$ncode-sha256$AAAA")]
    [InlineData("$ncode-sha256$47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU$extra")]
    public void TryParse_WhenTheSha256TextIsMalformed_ReturnsFalse(string text)
    {
        AssertRefused(text);
    }

    [Fact]
    public void TryParse_WhenTheDigestHasTheWrongLength_ReturnsFalse()
    {
        AssertRefused("$ncode-sha256$" + Base64Url.EncodeToString(new byte[31]));
        AssertRefused("$ncode-sha256$" + Base64Url.EncodeToString(new byte[33]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData("1e3")]
    [InlineData("")]
    [InlineData("99999999999")]
    public void TryParse_WhenThePbkdf2IterationsAreInvalid_ReturnsFalse(string iterations)
    {
        var salt = Base64Url.EncodeToString(new byte[16]);
        var key = Base64Url.EncodeToString(new byte[32]);

        AssertRefused($"$ncode-pbkdf2-sha256${iterations}${salt}${key}");
    }

    [Theory]
    [InlineData(7, 32)]
    [InlineData(257, 32)]
    [InlineData(16, 15)]
    [InlineData(16, 257)]
    public void TryParse_WhenThePbkdf2SaltOrKeyLengthIsOutOfRange_ReturnsFalse(
        int saltLength,
        int keyLength
    )
    {
        var salt = Base64Url.EncodeToString(new byte[saltLength]);
        var key = Base64Url.EncodeToString(new byte[keyLength]);

        AssertRefused($"$ncode-pbkdf2-sha256$1000${salt}${key}");
    }

    [Theory]
    [InlineData("$ncode-pbkdf2-sha256$1000")]
    [InlineData("$ncode-pbkdf2-sha256$1000$AAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$ncode-pbkdf2-sha256$1000$AAAAAAAAAAAAAAAAAAAAAA$")]
    [InlineData("$ncode-pbkdf2-sha256$1000$$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(
        "$ncode-pbkdf2-sha256$1000$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA$x"
    )]
    [InlineData("$ncode-pbkdf2-sha256$1000$!!!!$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void TryParse_WhenThePbkdf2TextIsMalformed_ReturnsFalse(string text)
    {
        AssertRefused(text);
    }

    #endregion

    #region SecretVerifier Tests

    [Fact]
    public void ToString_NamesTheKeyIdAndTheSchemeAndNothingElse()
    {
        var text = Sha256Text(Secret);
        var verifier = Parse(text);

        var formatted = verifier.ToString();

        Assert.Equal("SecretVerifier { KeyId = 'secret-1', Scheme = 'ncode-sha256' }", formatted);
        Assert.DoesNotContain(text["$ncode-sha256$".Length..], formatted, StringComparison.Ordinal);
    }

    #endregion
}
