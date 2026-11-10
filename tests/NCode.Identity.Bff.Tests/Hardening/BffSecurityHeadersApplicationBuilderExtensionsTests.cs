// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hardening;

public class BffSecurityHeadersApplicationBuilderExtensionsTests : BaseTests
{
    #region UseBffSecurityHeaders Tests

    [Fact]
    public async Task UseBffSecurityHeaders_Pipeline_WritesTheConfiguredHeaders()
    {
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton(
                BffTestFrontends.CreateCatalog(x =>
                    x.SecurityHeaders.ReferrerPolicy = "no-referrer"
                )
            )
            .BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider);

        var returned = app.UseBffSecurityHeaders();
        var pipeline = app.Build();

        var responseFeature = new StartingResponseFeature();
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(responseFeature);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        var httpContext = new DefaultHttpContext(features) { RequestServices = serviceProvider };

        await pipeline(httpContext);
        await responseFeature.StartAsync(CancellationToken.None);

        Assert.Same(app, returned);
        Assert.Equal("no-referrer", httpContext.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("nosniff", httpContext.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    public void UseBffSecurityHeaders_NullBuilder_Throws()
    {
        IApplicationBuilder app = null!;

        Assert.Throws<ArgumentNullException>(() => app.UseBffSecurityHeaders());
    }

    #endregion
}
