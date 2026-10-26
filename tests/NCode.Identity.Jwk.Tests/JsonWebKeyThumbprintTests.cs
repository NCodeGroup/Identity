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

    [Fact]
    public void ComputeThumbprintUri_WhenSha384_NamesTheHash()
    {
        var jwk = JsonWebKey.Parse($$"""{"kty":"RSA","n":"{{Rfc7638Modulus}}","e":"AQAB"}""");

        Assert.StartsWith(
            "urn:ietf:params:oauth:jwk-thumbprint:sha-384:",
            jwk.ComputeThumbprintUri(HashAlgorithmName.SHA384)
        );
    }
}
