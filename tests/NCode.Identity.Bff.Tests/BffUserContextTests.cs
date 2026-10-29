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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffUserContextTests : BaseTests
{
    private static BffUserContext CreateContext() =>
        new(new DefaultHttpContext(), new ClaimsPrincipal(), new BffSessionMetadata());

    #region AddField Tests

    [Theory]
    [InlineData("sub")]
    [InlineData("name")]
    [InlineData("claims")]
    [InlineData("session")]
    [InlineData("logoutUrl")]
    public void AddField_WithAReservedName_Throws(string name)
    {
        var context = CreateContext();

        var exception = Assert.Throws<ArgumentException>(() => context.AddField(name, "x"));

        Assert.Equal("name", exception.ParamName);
        Assert.Empty(context.Fields);
    }

    [Fact]
    public void AddField_WithANameAlreadyAdded_Throws()
    {
        var context = CreateContext();
        context.AddField("tenant", "acme");

        var exception = Assert.Throws<ArgumentException>(() => context.AddField("tenant", "other"));

        Assert.Equal("name", exception.ParamName);
        Assert.Equal("acme", context.Fields["tenant"]?.GetValue<string>());
    }

    [Fact]
    public void AddField_WithNewNames_KeepsTheirOrder()
    {
        var context = CreateContext();

        context.AddField("b", 1);
        context.AddField("a", 2);

        Assert.Equal(["b", "a"], context.Fields.Keys);
    }

    #endregion
}
