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

using System.Text.Json;
using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyTests
{
    [Fact]
    public void Parse_ExposesStandardMembersAndKeyMaterialByName()
    {
        var jwk = JsonWebKey.Parse(
            """{"kty":"RSA","use":"sig","alg":"RS256","kid":"k1","n":"bW9k","e":"AQAB"}"""
        );

        Assert.Equal("RSA", jwk.KeyType);
        Assert.Equal("sig", jwk.PublicKeyUse);
        Assert.Equal("RS256", jwk.Algorithm);
        Assert.Equal("k1", jwk.KeyId);
        Assert.Equal("bW9k", jwk.GetMember("n"));
        Assert.Equal("AQAB", jwk.GetMember("e"));
    }

    [Fact]
    public void Parse_WhenMissingKeyType_Throws()
    {
        Assert.Throws<ArgumentException>(() => JsonWebKey.Parse("""{"n":"bW9k","e":"AQAB"}"""));
    }

    [Fact]
    public void Serialize_RoundTripsThroughSystemTextJson()
    {
        const string json = """{"kty":"EC","crv":"P-256","x":"eA","y":"eQ","kid":"k2"}""";
        var jwk = JsonSerializer.Deserialize<JsonWebKey>(json);
        Assert.NotNull(jwk);

        var reserialized = JsonSerializer.Serialize(jwk);
        var jsonElement = JsonElement.Parse(reserialized);
        var root = jsonElement;

        Assert.Equal("EC", root.GetProperty("kty").GetString());
        Assert.Equal("P-256", root.GetProperty("crv").GetString());
        Assert.Equal("k2", root.GetProperty("kid").GetString());
    }

    [Fact]
    public void ToPublicKey_StripsPrivateMembers()
    {
        var jwk = JsonWebKey.Parse(
            """{"kty":"EC","crv":"P-256","x":"eA","y":"eQ","d":"c2VjcmV0"}"""
        );

        Assert.True(jwk.ContainsPrivateKeyMaterial);

        var publicKey = jwk.ToPublicKey();

        Assert.False(publicKey.ContainsPrivateKeyMaterial);
        Assert.Null(publicKey.GetMember("d"));
        Assert.Equal("eA", publicKey.GetMember("x"));
    }

    [Fact]
    public void JsonWebKeySet_RoundTripsKeys()
    {
        const string json =
            """{"keys":[{"kty":"RSA","n":"bW9k","e":"AQAB","kid":"a"},{"kty":"oct","k":"c2VjcmV0","kid":"b"}]}""";

        var set = JsonSerializer.Deserialize<JsonWebKeySet>(json);
        Assert.NotNull(set);

        Assert.Equal(2, set.Keys.Length);
        Assert.True(set.TryGetKey("a", out var first));
        Assert.Equal("RSA", first?.KeyType);

        var reserialized = JsonSerializer.Serialize(set);
        var jsonElement = JsonElement.Parse(reserialized);
        Assert.Equal(2, jsonElement.GetProperty("keys").GetArrayLength());
    }
}
