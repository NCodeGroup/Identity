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
using System.Security.Cryptography;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyThumbprintTests
{
    // The canonical RFC 7638 §3.1 worked example (RSA): the JWK and its known SHA-256 thumbprint.
    private const string Rfc7638Modulus =
        "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4"
        + "cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4"
        + "Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7"
        + "d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3X"
        + "PksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw";

    private const string Rfc7638Thumbprint = "NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs";

    [Fact]
    public void ComputeThumbprint_WhenRfc7638RsaExample_MatchesPublishedVector()
    {
        var jwk = JsonWebKey.Parse(
            $$"""
            {"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB","alg":"RS256","kid":"2011-04-29"}
            """
        );

        Assert.Equal(Rfc7638Thumbprint, jwk.ComputeThumbprint());
    }

    [Fact]
    public void ComputeThumbprint_IgnoresOptionalMembersAndMemberOrder()
    {
        var withOptional = JsonWebKey.Parse(
            $$"""
            {"use":"sig","kid":"abc","e":"AQAB","alg":"RS256","kty":"RSA","n":"{{Rfc7638Modulus}}"}
            """
        );
        var minimal = JsonWebKey.Parse($$"""{"kty":"RSA","e":"AQAB","n":"{{Rfc7638Modulus}}"}""");

        Assert.Equal(minimal.ComputeThumbprint(), withOptional.ComputeThumbprint());
    }

    [Fact]
    public void ComputeThumbprint_WhenEcKey_MatchesCanonicalComputation()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var x = Base64Url.EncodeToString(
            parameters.Q.X ?? throw new InvalidOperationException("missing X")
        );
        var y = Base64Url.EncodeToString(
            parameters.Q.Y ?? throw new InvalidOperationException("missing Y")
        );
        var jwk = JsonWebKey.Parse($$"""{"kty":"EC","crv":"P-256","x":"{{x}}","y":"{{y}}"}""");

        var canonical = $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""";
        var expected = Base64Url.EncodeToString(
            SHA256.HashData(SecureEncoding.UTF8.GetBytes(canonical))
        );

        Assert.Equal(expected, jwk.ComputeThumbprint());
    }

    [Fact]
    public void ComputeThumbprint_WhenUnsupportedKeyType_Throws()
    {
        var jwk = JsonWebKey.Parse("""{"kty":"UNKNOWN","x":"abc"}""");

        Assert.Throws<NotSupportedException>(() => jwk.ComputeThumbprint());
    }

    [Fact]
    public void ComputeThumbprint_WhenRequiredMemberMissing_Throws()
    {
        var jwk = JsonWebKey.Parse("""{"kty":"RSA","e":"AQAB"}""");

        Assert.Throws<InvalidOperationException>(() => jwk.ComputeThumbprint());
    }

    [Fact]
    public void ComputeThumbprintUri_WhenRfc7638RsaExample_UsesRfc9278Format()
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.Equal(
            $"urn:ietf:params:oauth:jwk-thumbprint:sha-256:{Rfc7638Thumbprint}",
            jwk.ComputeThumbprintUri()
        );
    }

    [Theory]
    [InlineData("SHA256", "sha-256")]
    [InlineData("SHA384", "sha-384")]
    [InlineData("SHA512", "sha-512")]
    public void ComputeThumbprintUri_WhenSha2Hash_UsesItsRegistryName(
        string algorithm,
        string hashName
    )
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");
        var hash = new HashAlgorithmName(algorithm);

        Assert.Equal(
            $"{JsonWebKey.ThumbprintUriPrefix}{hashName}:{jwk.ComputeThumbprint(hash)}",
            jwk.ComputeThumbprintUri(hash)
        );
    }

    [Theory]
    [InlineData("SHA3-256", "sha3-256")]
    [InlineData("SHA3-384", "sha3-384")]
    [InlineData("SHA3-512", "sha3-512")]
    public void ComputeThumbprintUri_WhenSha3Hash_UsesItsRegistryName(
        string algorithm,
        string hashName
    )
    {
        // Some Windows versions lack SHA-3.
        if (!SHA3_256.IsSupported || !SHA3_384.IsSupported || !SHA3_512.IsSupported)
            return;

        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");
        var hash = new HashAlgorithmName(algorithm);

        Assert.Equal(
            $"{JsonWebKey.ThumbprintUriPrefix}{hashName}:{jwk.ComputeThumbprint(hash)}",
            jwk.ComputeThumbprintUri(hash)
        );
    }

    [Fact]
    public void ComputeThumbprintUri_WhenHashHasNoRegistryName_ThrowsNotSupported()
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.Throws<NotSupportedException>(() =>
            jwk.ComputeThumbprintUri(HashAlgorithmName.SHA1)
        );
    }

    [Fact]
    public void ComputeThumbprintUri_WhenHashNameSupplied_UsesIt()
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.Equal(
            $"{JsonWebKey.ThumbprintUriPrefix}sha-1:{jwk.ComputeThumbprint(HashAlgorithmName.SHA1)}",
            jwk.ComputeThumbprintUri(HashAlgorithmName.SHA1, "sha-1")
        );
    }

    [Theory]
    [InlineData("sha-512")]
    [InlineData("sha-256-128")]
    public void ComputeThumbprintUri_WhenHashNameNamesAnotherHash_Throws(string hashName)
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.Throws<ArgumentException>(() =>
            jwk.ComputeThumbprintUri(HashAlgorithmName.SHA256, hashName)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha:1")]
    [InlineData("sha 1")]
    public void ComputeThumbprintUri_WhenHashNameNotUnreserved_Throws(string hashName)
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.Throws<ArgumentException>(() =>
            jwk.ComputeThumbprintUri(HashAlgorithmName.SHA1, hashName)
        );
    }
}
