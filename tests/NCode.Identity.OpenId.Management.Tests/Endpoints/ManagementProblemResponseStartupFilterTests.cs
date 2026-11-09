// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ManagementProblemResponseStartupFilterTests
{
    #region Configure Tests

    [Fact]
    public async Task Configure_Always_PutsTheMiddlewareBeforeTheHostsPipeline()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        new ManagementProblemResponseStartupFilter().Configure(builder =>
            builder.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            })
        )(app);

        var pipeline = app.Build();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Request = { Path = "/api/missing" },
            Response = { Body = new MemoryStream() },
        };

        await pipeline(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);
        httpContext.Response.Body.Position = 0;
        var root = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        Assert.Equal(StatusCodes.Status404NotFound, root.GetProperty("status").GetInt32());
    }

    #endregion
}
