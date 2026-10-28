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

public class JsonWebKeySetTests
{
    private static readonly JsonWebKeyTypeDescriptor XkpType = new("XKP", ["pub"], ["sk"]);

    private const string KeySetJson =
        """{"keys":[{"kty":"XKP","pub":"AQID","sk":"BAUG","kid":"x"},{"kty":"RSA","n":"AQ","e":"AQAB","kid":"r"}]}""";

    [Fact]
    public void Parse_WhenDescriptorSupplied_TypesTheContributedKey()
    {
        var set = JsonWebKeySet.Parse(KeySetJson, [XkpType]);

        Assert.True(set.TryGetKey("x", out var key));
        Assert.NotNull(key);
        Assert.Same(XkpType, key.KeyTypeDescriptor);
        Assert.True(key.ContainsPrivateKeyMaterial);
    }

    [Fact]
    public void Parse_WhenDescriptorSupplied_KeepsStandardDescriptors()
    {
        var set = JsonWebKeySet.Parse(KeySetJson, [XkpType]);

        Assert.True(set.TryGetKey("r", out var key));
        Assert.NotNull(key);
        Assert.Same(JsonWebKeyTypeDescriptors.Rsa, key.KeyTypeDescriptor);
    }

    [Fact]
    public void Parse_WhenNoDescriptorSupplied_LeavesContributedKeyUntyped()
    {
        var set = JsonWebKeySet.Parse(KeySetJson);

        Assert.True(set.TryGetKey("x", out var key));
        Assert.NotNull(key);
        Assert.Null(key.KeyTypeDescriptor);
    }

    [Fact]
    public void Parse_WhenDescriptorNamesStandardType_UsesIt()
    {
        var custom = new JsonWebKeyTypeDescriptor(JsonWebKeyTypes.Rsa, ["e", "n"], ["d"]);

        var set = JsonWebKeySet.Parse(KeySetJson, [custom]);

        Assert.True(set.TryGetKey("r", out var key));
        Assert.NotNull(key);
        Assert.Same(custom, key.KeyTypeDescriptor);
    }

    [Fact]
    public void Constructor_WhenDescriptorNamesRepeat_Throws()
    {
        var element = JsonElement.Parse(KeySetJson);

        Assert.Throws<ArgumentException>(() =>
            new JsonWebKeySet(element, [XkpType, new JsonWebKeyTypeDescriptor("XKP", ["pub"], [])])
        );
    }

    [Fact]
    public void Constructor_WhenDescriptorIsNull_Throws()
    {
        var element = JsonElement.Parse(KeySetJson);

        Assert.Throws<ArgumentException>(() => new JsonWebKeySet(element, [XkpType, null!]));
    }

    [Fact]
    public void Parse_WhenKeyTypeEmpty_SkipsTheKey()
    {
        var set = JsonWebKeySet.Parse("""{"keys":[{"kty":""},{"kty":"oct","k":"AQ"}]}""");

        Assert.Single(set.Keys);
    }
}
