// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
