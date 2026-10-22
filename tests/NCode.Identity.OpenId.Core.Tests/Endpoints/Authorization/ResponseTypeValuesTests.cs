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

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization;

public class ResponseTypeValuesTests
{
    #region Canonicalize Tests

    [Fact]
    public void Canonicalize_WhenGivenNames_OrdersThemOrdinally()
    {
        string[] names = ["token", "code", "id_token"];

        var result = ResponseTypeValues.Canonicalize(names);

        Assert.Equal(OpenIdConstants.ResponseTypes.CodeIdTokenToken, result);
    }

    [Theory]
    [InlineData("code", "code")]
    [InlineData("id_token code", "code id_token")]
    [InlineData("token id_token", "id_token token")]
    [InlineData("token id_token code", "code id_token token")]
    [InlineData("code code", "code code")]
    public void Canonicalize_WhenGivenAValue_OrdersItsNames(string value, string expected)
    {
        var result = ResponseTypeValues.Canonicalize(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("id_token  code")]
    [InlineData(" code")]
    [InlineData("code ")]
    [InlineData("")]
    public void Canonicalize_WhenNamesAreNotSeparatedBySingleSpaces_ReturnsTheValueAsWritten(
        string value
    )
    {
        var result = ResponseTypeValues.Canonicalize(value);

        Assert.Equal(value, result);
    }

    #endregion

    #region ReturnsTokens Tests

    [Theory]
    [InlineData(new[] { "code" }, false)]
    [InlineData(new[] { "id_token" }, true)]
    [InlineData(new[] { "token" }, true)]
    [InlineData(new[] { "code", "id_token" }, true)]
    [InlineData(new[] { "token", "code" }, true)]
    [InlineData(new string[0], false)]
    public void ReturnsTokens_IsTrueOnlyWhenTheResponseTypeIncludesATokenName(
        string[] names,
        bool expected
    )
    {
        var result = ResponseTypeValues.ReturnsTokens(names);

        Assert.Equal(expected, result);
    }

    #endregion

    #region GetDefaultResponseMode Tests

    [Theory]
    [InlineData(new[] { "code" }, "query")]
    [InlineData(new[] { "id_token" }, "fragment")]
    [InlineData(new[] { "id_token", "token" }, "fragment")]
    [InlineData(new[] { "code", "token" }, "fragment")]
    [InlineData(new string[0], "query")]
    public void GetDefaultResponseMode_ReturnsTheFragmentForAResponseTypeThatReturnsTokens(
        string[] names,
        string expected
    )
    {
        var result = ResponseTypeValues.GetDefaultResponseMode(names);

        Assert.Equal(expected, result);
    }

    #endregion

    #region GetGrantTypes Tests

    [Theory]
    [InlineData("code", new[] { "authorization_code" })]
    [InlineData("id_token", new[] { "implicit" })]
    [InlineData("token", new[] { "implicit" })]
    [InlineData("id_token token", new[] { "implicit" })]
    [InlineData("code id_token", new[] { "authorization_code", "implicit" })]
    [InlineData("code token", new[] { "authorization_code", "implicit" })]
    [InlineData("code id_token token", new[] { "authorization_code", "implicit" })]
    [InlineData("token code", new[] { "authorization_code", "implicit" })]
    [InlineData("none", new string[0])]
    public void GetGrantTypes_ReturnsTheGrantTypesTheResponseTypeUses(
        string value,
        string[] expected
    )
    {
        var result = ResponseTypeValues.GetGrantTypes(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetGrantTypes_WhenGivenNames_ReturnsTheGrantTypesTheyUse()
    {
        string[] names =
        [
            OpenIdConstants.ResponseTypes.IdToken,
            OpenIdConstants.ResponseTypes.Code,
        ];

        var result = ResponseTypeValues.GetGrantTypes(names);

        Assert.Equal(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            result
        );
    }

    #endregion
}
