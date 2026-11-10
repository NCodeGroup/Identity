// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public class SecretVerifierFormatTests
{
    private static KeyMetadata Metadata { get; } =
        new() { KeyId = "secret-1", ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1) };

    private static byte[] Secret { get; } = "the-client-secret"u8.ToArray();
    private static byte[] OtherSecret { get; } = "the-client-secreT"u8.ToArray();

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

    private static DefaultSecretVerifierSchemeCatalog Catalog { get; } =
        TestSecretVerifierSchemes.CreateCatalog();

    private static ISecretVerifierScheme GetScheme(string name)
    {
        Assert.True(Catalog.TryGet(name, out var scheme));
        return scheme;
    }

    private static byte[] Write(string schemeName, byte[] secret)
    {
        var writer = new ArrayBufferWriter<byte>();
        SecretVerifierFormat.Write(GetScheme(schemeName), secret, writer);
        return writer.WrittenSpan.ToArray();
    }

    private static SecretVerifier Parse(string text) => Parse(SecureEncoding.ASCII.GetBytes(text));

    private static SecretVerifier Parse(byte[] text)
    {
        Assert.True(SecretVerifierFormat.TryParse(Catalog, Metadata, text, out var verifier));
        return verifier;
    }

    private static void AssertRefused(string text)
    {
        Assert.False(
            SecretVerifierFormat.TryParse(
                Catalog,
                Metadata,
                SecureEncoding.ASCII.GetBytes(text),
                out var verifier
            )
        );
        Assert.Null(verifier);
    }

    private static async ValueTask<bool> VerifyAsync(
        SecretVerifier verifier,
        byte[] secret,
        CancellationToken cancellationToken
    ) => await verifier.VerifyAsync(secret, cancellationToken) == SecretVerificationResult.Success;

    #region Write Tests

    [Fact]
    public void Write_WhenSha256_WritesTheSchemeAndTheDigest()
    {
        var text = Write(SecretVerifierSchemes.Sha256, Secret);

        Assert.Equal(Sha256Text(Secret), SecureEncoding.ASCII.GetString(text));
    }

    [Fact]
    public async Task Write_WhenSha256_RoundTripsThroughTryParse()
    {
        var verifier = Parse(Write(SecretVerifierSchemes.Sha256, Secret));

        Assert.Equal(SecretVerifierSchemes.Sha256, verifier.Scheme);
        Assert.Equal("secret-1", verifier.KeyId);
        Assert.Equal(Metadata, verifier.Metadata);
        Assert.True(await VerifyAsync(verifier, Secret, CancellationToken.None));
        Assert.False(await VerifyAsync(verifier, OtherSecret, CancellationToken.None));
        Assert.False(await VerifyAsync(verifier, [], CancellationToken.None));
    }

    [Fact]
    public void Write_WhenPbkdf2_WritesTheSchemeTheIterationsTheSaltAndTheDerivedKey()
    {
        var text = Write(SecretVerifierSchemes.Pbkdf2Sha256, Secret);

        var segments = SecureEncoding.ASCII.GetString(text).Split('$');
        Assert.Equal(5, segments.Length);
        Assert.Empty(segments[0]);
        Assert.Equal("ncode-pbkdf2-sha256", segments[1]);
        Assert.Equal("1000", segments[2]);

        var salt = Base64Url.DecodeFromChars(segments[3]);
        Assert.Equal(Pbkdf2SecretVerifierScheme.SaltByteCount, salt.Length);
        var expectedKey = Rfc2898DeriveBytes.Pbkdf2(
            Secret,
            salt,
            1_000,
            HashAlgorithmName.SHA256,
            Pbkdf2SecretVerifierScheme.DerivedKeyByteCount
        );
        Assert.Equal(expectedKey, Base64Url.DecodeFromChars(segments[4]));
    }

    [Fact]
    public void Write_WhenPbkdf2_DrawsAFreshSaltEachTime()
    {
        var first = Write(SecretVerifierSchemes.Pbkdf2Sha256, Secret);
        var second = Write(SecretVerifierSchemes.Pbkdf2Sha256, Secret);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Write_WhenPbkdf2_RoundTripsThroughTryParse()
    {
        var verifier = Parse(Write(SecretVerifierSchemes.Pbkdf2Sha256, Secret));

        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.True(await VerifyAsync(verifier, Secret, CancellationToken.None));
        Assert.False(await VerifyAsync(verifier, OtherSecret, CancellationToken.None));
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public async Task TryParse_ReadsAPbkdf2VerifierOfTheFamilysPasswordHashFormatWithOtherParameters()
    {
        // The parameters come from the text, so a verifier made with other parameters keeps verifying.
        var verifier = Parse(Pbkdf2Text(Secret, iterations: 1_234, saltLength: 32, keyLength: 64));

        Assert.True(await VerifyAsync(verifier, Secret, CancellationToken.None));
        Assert.False(await VerifyAsync(verifier, OtherSecret, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ncode-sha256$AAAA")]
    [InlineData("$ncode-sha256")]
    [InlineData("$unknown-scheme$AAAA")]
    [InlineData("$$AAAA")]
    [InlineData("$ncode sha256$AAAA")]
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
