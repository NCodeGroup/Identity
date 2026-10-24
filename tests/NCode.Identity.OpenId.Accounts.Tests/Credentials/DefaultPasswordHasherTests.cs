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

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Accounts.Credentials;

public sealed class DefaultPasswordHasherTests
{
    private const string ComposedPassword = "caf\u00E9 correct horse battery";
    private const string DecomposedPassword = "cafe\u0301 correct horse battery";

    private static readonly byte[] PasswordBytes = "correct horse battery staple"u8.ToArray();
    private static readonly byte[] ComposedBytes = "caf\u00E9 correct horse battery"u8.ToArray();
    private static readonly byte[] DecomposedBytes = "cafe\u0301 correct horse battery"u8.ToArray();
    private static readonly byte[] WrongDecomposedBytes =
        "cafe\u0301 wrong horse battery"u8.ToArray();

    private static DefaultPasswordHasher CreateHasher(int iterations = 1000) =>
        new(Options.Create(new PasswordHasherOptions { Iterations = iterations }));

    private static string HashSubmittedForm(string password, int iterations)
    {
        byte[] salt = "0123456789abcdef"u8.ToArray();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32
        );

        var iterationsText = iterations.ToString(CultureInfo.InvariantCulture);
        var saltText = Base64Url.EncodeToString(salt);
        var keyText = Base64Url.EncodeToString(key);
        return string.Concat("$ncode-pbkdf2-sha256$", iterationsText, "$", saltText, "$", keyText);
    }

    #region DummyPasswordHash Property Tests

    [Fact]
    public void DummyPasswordHash_HasTheCurrentIterations()
    {
        var hasher = CreateHasher();

        Assert.StartsWith("$ncode-pbkdf2-sha256$1000$", hasher.DummyPasswordHash);
    }

    [Fact]
    public void DummyPasswordHash_DiffersBetweenHashers()
    {
        var first = CreateHasher();
        var second = CreateHasher();

        Assert.NotEqual(first.DummyPasswordHash, second.DummyPasswordHash);
    }

    #endregion

    #region HashPassword Tests

    [Fact]
    public void HashPassword_WritesTheSchemeAndTheIterations()
    {
        var hasher = CreateHasher();

        var hash = hasher.HashPassword(PasswordBytes);

        Assert.StartsWith("$ncode-pbkdf2-sha256$1000$", hash);
    }

    #endregion

    #region VerifyHashedPassword Tests

    [Fact]
    public void VerifyHashedPassword_WhenThePasswordMatches_Succeeds()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(PasswordBytes);

        var result = hasher.VerifyHashedPassword(hash, PasswordBytes);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenThePasswordIsWrong_Fails()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(PasswordBytes);

        var result = hasher.VerifyHashedPassword(hash, "wrong horse battery staple"u8);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenTheHashHasFewerIterations_RequestsARehash()
    {
        var hash = CreateHasher(1000).HashPassword(PasswordBytes);
        var hasher = CreateHasher(2000);

        var result = hasher.VerifyHashedPassword(hash, PasswordBytes);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenTheHashIsMalformed_Fails()
    {
        var hasher = CreateHasher();

        var result = hasher.VerifyHashedPassword("not-a-password-hash", PasswordBytes);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenGivenTheDummyHash_Fails()
    {
        var hasher = CreateHasher();
        var dummyHash = hasher.DummyPasswordHash;

        var result = hasher.VerifyHashedPassword(dummyHash, PasswordBytes);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenTheComposedFormIsSubmitted_VerifiesAHashOfTheDecomposedForm()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(DecomposedBytes);

        var result = hasher.VerifyHashedPassword(hash, ComposedBytes);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenTheDecomposedFormIsSubmitted_VerifiesAHashOfTheComposedForm()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(ComposedBytes);

        var result = hasher.VerifyHashedPassword(hash, DecomposedBytes);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenOnlyTheSubmittedFormMatches_RequestsARehash()
    {
        var hasher = CreateHasher(1000);
        var hash = HashSubmittedForm(DecomposedPassword, 1000);

        var result = hasher.VerifyHashedPassword(hash, DecomposedBytes);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenAWrongPasswordIsNotInNfc_Fails()
    {
        var hasher = CreateHasher(1000);
        var hash = HashSubmittedForm(DecomposedPassword, 1000);

        var result = hasher.VerifyHashedPassword(hash, WrongDecomposedBytes);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenASubmittedFormHashIsComposed_Succeeds()
    {
        var hasher = CreateHasher(1000);
        var hash = HashSubmittedForm(ComposedPassword, 1000);

        var result = hasher.VerifyHashedPassword(hash, ComposedBytes);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenThePasswordIsNotUtf8_VerifiesTheBytesAsTheyAre()
    {
        byte[] password = [0xFF, 0xFE, 0x41];
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(password);

        var result = hasher.VerifyHashedPassword(hash, password);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    #endregion
}
