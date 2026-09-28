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
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Endpoints.Jwks.Results;

public class JsonWebKeySerializationTests
{
    #region RsaJsonWebKey Tests

    [Fact]
    public void Serialize_GivenRsaJsonWebKey_ThenWritesExpectedMembers()
    {
        JsonWebKey key = new RsaJsonWebKey
        {
            KeyId = "kid-1",
            Use = "sig",
            Algorithm = "RS256",
            Modulus = "modulus",
            Exponent = "AQAB",
        };

        using var document = JsonSerializer.SerializeToDocument(key);
        var root = document.RootElement;

        Assert.Equal("RSA", root.GetProperty("kty").GetString());
        Assert.Equal("sig", root.GetProperty("use").GetString());
        Assert.Equal("kid-1", root.GetProperty("kid").GetString());
        Assert.Equal("RS256", root.GetProperty("alg").GetString());
        Assert.Equal("modulus", root.GetProperty("n").GetString());
        Assert.Equal("AQAB", root.GetProperty("e").GetString());
    }

    [Fact]
    public void Serialize_GivenRsaJsonWebKey_ThenKtyIsFirstMember()
    {
        JsonWebKey key = new RsaJsonWebKey { Modulus = "modulus", Exponent = "AQAB" };

        using var document = JsonSerializer.SerializeToDocument(key);
        var firstPropertyName = document.RootElement.EnumerateObject().First().Name;

        Assert.Equal("kty", firstPropertyName);
    }

    [Fact]
    public void Serialize_GivenRsaJsonWebKeyWithoutOptionalMembers_ThenOmitsNullMembers()
    {
        JsonWebKey key = new RsaJsonWebKey { Modulus = "modulus", Exponent = "AQAB" };

        using var document = JsonSerializer.SerializeToDocument(key);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("use", out _));
        Assert.False(root.TryGetProperty("kid", out _));
        Assert.False(root.TryGetProperty("alg", out _));
    }

    #endregion

    #region EccJsonWebKey Tests

    [Fact]
    public void Serialize_GivenEccJsonWebKey_ThenWritesExpectedMembers()
    {
        JsonWebKey key = new EccJsonWebKey
        {
            KeyId = "kid-2",
            Curve = "P-256",
            X = "x-coordinate",
            Y = "y-coordinate",
        };

        using var document = JsonSerializer.SerializeToDocument(key);
        var root = document.RootElement;

        Assert.Equal("EC", root.GetProperty("kty").GetString());
        Assert.Equal("kid-2", root.GetProperty("kid").GetString());
        Assert.Equal("P-256", root.GetProperty("crv").GetString());
        Assert.Equal("x-coordinate", root.GetProperty("x").GetString());
        Assert.Equal("y-coordinate", root.GetProperty("y").GetString());
    }

    #endregion

    #region JsonWebKeySetResult Tests

    [Fact]
    public void Serialize_GivenKeySet_ThenWritesKeysArrayWithDiscriminatedItems()
    {
        var result = new JsonWebKeySetResult
        {
            Keys =
            [
                new RsaJsonWebKey { Modulus = "modulus", Exponent = "AQAB" },
                new EccJsonWebKey
                {
                    Curve = "P-256",
                    X = "x",
                    Y = "y",
                },
            ],
        };

        using var document = JsonSerializer.SerializeToDocument(result);
        var keys = document.RootElement.GetProperty("keys");

        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.Equal(2, keys.GetArrayLength());
        Assert.Equal("RSA", keys[0].GetProperty("kty").GetString());
        Assert.Equal("EC", keys[1].GetProperty("kty").GetString());
    }

    [Fact]
    public void Serialize_GivenEmptyKeySet_ThenWritesEmptyKeysArray()
    {
        var result = new JsonWebKeySetResult();

        using var document = JsonSerializer.SerializeToDocument(result);
        var keys = document.RootElement.GetProperty("keys");

        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.Empty(keys.EnumerateArray());
    }

    #endregion
}
