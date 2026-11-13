// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class OutboundUriRedactionTests : BaseTests
{
    #region ForLog Tests

    [Fact]
    public void ForLog_WhenTheUriHasAQueryAndFragment_ReturnsTheUriWithoutThem()
    {
        var uri = new Uri("https://example.com/path/doc.jwt?token=secret#frag");

        var result = OutboundUriRedaction.ForLog(uri);

        Assert.Equal("https://example.com/path/doc.jwt", result);
    }

    [Fact]
    public void ForLog_WhenTheUriHasNoQuery_ReturnsTheUri()
    {
        var uri = new Uri("https://example.com:8443/jwks");

        var result = OutboundUriRedaction.ForLog(uri);

        Assert.Equal("https://example.com:8443/jwks", result);
    }

    [Fact]
    public void ForLog_WhenTheStringIsAbsolute_ReturnsItWithoutTheQuery()
    {
        var result = OutboundUriRedaction.ForLog(
            "https://example.com/path/doc.jwt?token=secret#frag"
        );

        Assert.Equal("https://example.com/path/doc.jwt", result);
    }

    [Fact]
    public void ForLog_WhenTheStringIsNotAbsolute_ReturnsTheTextBeforeTheQuery()
    {
        var result = OutboundUriRedaction.ForLog("/relative/path?x=1");

        Assert.Equal("/relative/path", result);
    }

    #endregion
}
