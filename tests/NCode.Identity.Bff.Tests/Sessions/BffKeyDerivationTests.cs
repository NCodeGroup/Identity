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

using System.Security.Cryptography;
using NCode.Encoders;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffKeyDerivationTests : BaseTests
{
    #region CreateSecret Tests

    [Fact]
    public void CreateSecret_Always_ReturnsADecodableSecretOfTheFixedLength()
    {
        var secret = BffKeyDerivation.CreateSecret();

        var bytes = new byte[32];
        Assert.Equal(BffKeyDerivation.SecretLength, secret.Length);
        Assert.True(BffKeyDerivation.TryDecodeSecret(secret, bytes));
    }

    #endregion

    #region TryDeriveKey Tests

    [Fact]
    public void TryDeriveKey_SameSecretAndPurpose_IsDeterministic()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var first = new byte[32];
        var second = new byte[32];

        Assert.True(
            BffKeyDerivation.TryDeriveKey(secret, BffKeyDerivation.SessionPayloadPurpose, first)
        );
        Assert.True(
            BffKeyDerivation.TryDeriveKey(secret, BffKeyDerivation.SessionPayloadPurpose, second)
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void TryDeriveKey_DifferentPurpose_DiffersPerPurpose()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var first = new byte[32];
        var second = new byte[32];

        Assert.True(
            BffKeyDerivation.TryDeriveKey(secret, BffKeyDerivation.SessionPayloadPurpose, first)
        );
        Assert.True(
            BffKeyDerivation.TryDeriveKey(secret, BffKeyDerivation.LoginPayloadPurpose, second)
        );

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TryDeriveKey_NullSecret_ReturnsFalse()
    {
        var key = new byte[32];

        Assert.False(
            BffKeyDerivation.TryDeriveKey(null, BffKeyDerivation.SessionPayloadPurpose, key)
        );
    }

    [Fact]
    public void TryDeriveKey_SessionPayloadPurpose_IsHkdfOfTheUtf8Label()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var ikm = new byte[32];
        Assert.True(BffKeyDerivation.TryDecodeSecret(secret, ikm));
        var expected = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            32,
            [],
            "ncode.bff.session.payload"u8.ToArray()
        );
        var key = new byte[32];

        var result = BffKeyDerivation.TryDeriveKey(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            key
        );

        Assert.True(result);
        Assert.Equal(expected, key);
    }

    #endregion

    #region TryDeriveIdentifier Tests

    [Fact]
    public void TryDeriveIdentifier_ValidSecret_ReturnsDeterministicIdentifierPerPurpose()
    {
        var secret = BffKeyDerivation.CreateSecret();

        Assert.True(
            BffKeyDerivation.TryDeriveIdentifier(
                secret,
                BffKeyDerivation.SessionIdPurpose,
                out var first
            )
        );
        Assert.True(
            BffKeyDerivation.TryDeriveIdentifier(
                secret,
                BffKeyDerivation.SessionIdPurpose,
                out var second
            )
        );
        Assert.True(
            BffKeyDerivation.TryDeriveIdentifier(
                secret,
                BffKeyDerivation.LoginIdPurpose,
                out var other
            )
        );

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.NotEqual(secret, first);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(44)]
    public void TryDeriveIdentifier_WrongLength_ReturnsFalse(int length)
    {
        var secret = new string('A', length);

        var result = BffKeyDerivation.TryDeriveIdentifier(
            secret,
            BffKeyDerivation.SessionIdPurpose,
            out var identifier
        );

        Assert.False(result);
        Assert.Null(identifier);
    }

    [Fact]
    public void TryDeriveIdentifier_IllegalCharacter_ReturnsFalse()
    {
        var secret = new string('A', 42) + "!";

        var result = BffKeyDerivation.TryDeriveIdentifier(
            secret,
            BffKeyDerivation.SessionIdPurpose,
            out var identifier
        );

        Assert.False(result);
        Assert.Null(identifier);
    }

    #endregion

    #region Fingerprint Tests

    [Fact]
    public void Fingerprint_SameValue_IsDeterministicAndDiffersPerValue()
    {
        Assert.Equal(BffKeyDerivation.Fingerprint("a"), BffKeyDerivation.Fingerprint("a"));
        Assert.NotEqual(BffKeyDerivation.Fingerprint("a"), BffKeyDerivation.Fingerprint("b"));
        Assert.Equal(43, BffKeyDerivation.Fingerprint("a").Length);
    }

    [Fact]
    public void Fingerprint_Value_IsBase64UrlSha256OfUtf8()
    {
        var expected = Base64Url.Encode(SHA256.HashData("refresh-token-value"u8));

        var fingerprint = BffKeyDerivation.Fingerprint("refresh-token-value");

        Assert.Equal(expected, fingerprint);
    }

    #endregion
}
