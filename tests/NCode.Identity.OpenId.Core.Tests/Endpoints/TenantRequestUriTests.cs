// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class TenantRequestUriTests : BaseTests
{
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private DefaultHttpContext HttpContext { get; }

    public TenantRequestUriTests()
    {
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenant = CreateStrictMock<OpenIdTenant>();

        // The request names a host the tenant does not publish, so any URL built from it would differ.
        HttpContext = new DefaultHttpContext();
        HttpContext.Request.Scheme = "https";
        HttpContext.Request.Host = new HostString("attacker.test");
        HttpContext.Request.PathBase = "/acme";
        HttpContext.Request.Path = "/oauth2/token";

        MockContext.SetupGet(x => x.Http).Returns(HttpContext).Verifiable();
        MockContext.SetupGet(x => x.Tenant).Returns(MockTenant.Object).Verifiable();
        MockTenant
            .SetupGet(x => x.BaseAddress)
            .Returns(
                new UriDescriptor
                {
                    Scheme = "https",
                    Host = new HostString("op.test"),
                    Path = new PathString("/acme"),
                }
            )
            .Verifiable();
    }

    #region GetPublishedUris Tests

    [Fact]
    public void GetPublishedUris_WhenTheTenantHasNoMtlsAlias_ReturnsTheUrlUnderTheBaseAddressOnly()
    {
        MockTenant.SetupGet(x => x.MtlsEndpointAliasAuthority).Returns((string?)null).Verifiable();

        var uris = TenantRequestUri.GetPublishedUris(MockContext.Object);

        Assert.Equal(["https://op.test/acme/oauth2/token"], uris);
    }

    [Fact]
    public void GetPublishedUris_WhenTheTenantHasAnMtlsAlias_ReturnsTheUrlUnderTheBaseAddressThenUnderTheAlias()
    {
        MockTenant
            .SetupGet(x => x.MtlsEndpointAliasAuthority)
            .Returns("https://mtls.op.test:8443")
            .Verifiable();

        var uris = TenantRequestUri.GetPublishedUris(MockContext.Object);

        Assert.Equal(
            ["https://op.test/acme/oauth2/token", "https://mtls.op.test:8443/acme/oauth2/token"],
            uris
        );
    }

    #endregion
}
