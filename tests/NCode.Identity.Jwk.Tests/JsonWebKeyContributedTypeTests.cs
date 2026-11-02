// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyContributedTypeTests
{
    private static readonly JsonWebKeyTypeDescriptor XkpType = new("XKP", ["pub"], ["sk"]);

    private static JsonWebKey CreateXkp(string json) => new(JsonElement.Parse(json), XkpType);

    [Fact]
    public void ComputeThumbprint_WhenContributedType_UsesItsMembers()
    {
        var jwk = CreateXkp("""{"kty":"XKP","pub":"AQID","kid":"a"}""");

        var canonical = """{"kty":"XKP","pub":"AQID"}""";
        var expected = Base64Url.EncodeToString(
            SHA256.HashData(SecureEncoding.UTF8.GetBytes(canonical))
        );

        Assert.Equal(expected, jwk.ComputeThumbprint());
    }

    [Fact]
    public void ContainsPrivateKeyMaterial_WhenContributedPrivateMember_ReturnsTrue()
    {
        const string json = """{"kty":"XKP","pub":"AQID","sk":"BAUG"}""";

        Assert.True(CreateXkp(json).ContainsPrivateKeyMaterial);
        Assert.False(JsonWebKey.Parse(json).ContainsPrivateKeyMaterial);
    }

    [Fact]
    public void ToPublicKey_WhenContributedType_StripsItsPrivateMemberAndKeepsDescriptor()
    {
        var jwk = CreateXkp("""{"kty":"XKP","pub":"AQID","sk":"BAUG"}""");

        var publicKey = jwk.ToPublicKey();

        Assert.Null(publicKey.GetMember("sk"));
        Assert.Equal("AQID", publicKey.GetMember("pub"));
        Assert.Same(XkpType, publicKey.KeyTypeDescriptor);
    }

    [Fact]
    public void ContainsPrivateKeyMaterial_WhenContributedTypeCarriesStandardPrivateName_ReturnsTrue()
    {
        var jwk = CreateXkp("""{"kty":"XKP","pub":"AQID","d":"BAUG"}""");

        Assert.True(jwk.ContainsPrivateKeyMaterial);
    }

    [Fact]
    public void Constructor_WhenDescriptorNameDiffers_Throws()
    {
        var element = JsonElement.Parse("""{"kty":"RSA","n":"AQ","e":"AQAB"}""");

        Assert.Throws<ArgumentException>(() => new JsonWebKey(element, XkpType));
    }

    [Fact]
    public void ComputeThumbprint_WhenAkp_IncludesAlg()
    {
        var jwk = JsonWebKey.Parse("""{"kty":"AKP","alg":"ML-DSA-44","pub":"AQID"}""");

        var canonical = """{"alg":"ML-DSA-44","kty":"AKP","pub":"AQID"}""";
        var expected = Base64Url.EncodeToString(
            SHA256.HashData(SecureEncoding.UTF8.GetBytes(canonical))
        );

        Assert.Equal(expected, jwk.ComputeThumbprint());
    }
}
