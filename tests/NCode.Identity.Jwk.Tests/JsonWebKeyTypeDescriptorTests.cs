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

using Xunit;

namespace NCode.Identity.Jwk.Tests;

public class JsonWebKeyTypeDescriptorTests
{
    [Fact]
    public void Constructor_WhenMembersUnordered_SortsThemAndAddsKty()
    {
        var descriptor = new JsonWebKeyTypeDescriptor("EC", ["y", "x", "crv"], ["d"]);

        Assert.Equal(["crv", "kty", "x", "y"], descriptor.ThumbprintMembers.ToArray());
        Assert.Equal(["d"], descriptor.PrivateMembers.ToArray());
    }

    [Fact]
    public void Constructor_WhenNameEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JsonWebKeyTypeDescriptor("", ["x"], ["d"]));
    }

    [Fact]
    public void Constructor_WhenKtyIsPrivate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new JsonWebKeyTypeDescriptor("EC", ["x"], ["kty"]));
    }

    [Fact]
    public void TryGetStandard_WhenAkp_ThumbprintsAlgKtyPub()
    {
        var found = JsonWebKeyTypeDescriptors.TryGetStandard("AKP", out var descriptor);

        Assert.True(found);
        Assert.NotNull(descriptor);
        Assert.Equal(["alg", "kty", "pub"], descriptor.ThumbprintMembers.ToArray());
    }

    [Fact]
    public void Rsa_WhenStandard_ListsEveryRsaPrivateMember()
    {
        Assert.Equal(
            ["d", "dp", "dq", "oth", "p", "q", "qi"],
            JsonWebKeyTypeDescriptors.Rsa.PrivateMembers.Order(StringComparer.Ordinal).ToArray()
        );
    }

    [Fact]
    public void TryGetStandard_WhenUnknown_ReturnsFalse()
    {
        Assert.False(JsonWebKeyTypeDescriptors.TryGetStandard("XKP", out var descriptor));
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryGetStandard_WhenCaseDiffers_ReturnsFalse()
    {
        Assert.False(JsonWebKeyTypeDescriptors.TryGetStandard("rsa", out _));
    }
}
