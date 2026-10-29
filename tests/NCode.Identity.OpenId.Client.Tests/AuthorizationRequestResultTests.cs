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

namespace NCode.Identity.OpenId.Client.Tests;

public class AuthorizationRequestResultTests
{
    private static Uri CreateUri() => new("https://issuer.example/authorize?client_id=c1");

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenFrontChannel_IsSuccessAndNotPushed()
    {
        var uri = CreateUri();

        var result = new AuthorizationRequestResult(uri);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsPushed);
        Assert.Same(uri, result.AuthorizationUri);
        Assert.Null(result.RequestUri);
        Assert.Null(result.ExpiresIn);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Constructor_WhenPushed_ExposesRequestUriAndExpiresIn()
    {
        var uri = CreateUri();

        var result = new AuthorizationRequestResult(
            uri,
            "urn:ietf:params:oauth:request_uri:abc",
            60
        );

        Assert.True(result.IsSuccess);
        Assert.True(result.IsPushed);
        Assert.Same(uri, result.AuthorizationUri);
        Assert.Equal("urn:ietf:params:oauth:request_uri:abc", result.RequestUri);
        Assert.Equal(60, result.ExpiresIn);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Constructor_WhenRequestUriIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AuthorizationRequestResult(CreateUri(), "", 60));
    }

    [Fact]
    public void Constructor_WhenExpiresInIsZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthorizationRequestResult(CreateUri(), "urn:ietf:params:oauth:request_uri:abc", 0)
        );
    }

    [Fact]
    public void Constructor_WhenError_IsNotSuccess()
    {
        var error = new ProtocolError("invalid_request", 400);

        var result = new AuthorizationRequestResult(error);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPushed);
        Assert.Null(result.AuthorizationUri);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Constructor_WhenUriIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AuthorizationRequestResult((Uri)null!));
    }

    #endregion

    #region DPoPJwkThumbprint Tests

    [Fact]
    public void DPoPJwkThumbprint_WhenNotSet_ThenNull()
    {
        var result = new AuthorizationRequestResult(CreateUri());

        Assert.Null(result.DPoPJwkThumbprint);
    }

    [Fact]
    public void DPoPJwkThumbprint_WhenSet_ThenValue()
    {
        var result = new AuthorizationRequestResult(CreateUri()) { DPoPJwkThumbprint = "jkt-1" };

        Assert.Equal("jkt-1", result.DPoPJwkThumbprint);
    }

    #endregion
}
