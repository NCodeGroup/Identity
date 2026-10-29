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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffEndpointRouteBuilderExtensionsTests : BaseTests
{
    private static List<RouteEndpoint> MapEndpoints(Action<BffOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        var optionsBuilder = builder.Services.AddOptions<BffOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        var app = builder.Build();
        IEndpointRouteBuilder endpoints = app;

        endpoints.MapBffEndpoints();

        return endpoints.DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
    }

    #region MapBffEndpoints Tests

    [Fact]
    public void MapBffEndpoints_MapsTheThreeRoutesUnderThePrefix()
    {
        var endpoints = MapEndpoints();

        var patterns = endpoints.Select(x => x.RoutePattern.RawText).Order().ToList();

        Assert.Equal(["/bff/callback", "/bff/login", "/bff/user"], patterns);
    }

    [Fact]
    public void MapBffEndpoints_WithCustomPrefix_UsesIt()
    {
        var endpoints = MapEndpoints(x => x.PathPrefix = "/auth");

        var patterns = endpoints.Select(x => x.RoutePattern.RawText).Order().ToList();

        Assert.Equal(["/auth/callback", "/auth/login", "/auth/user"], patterns);
    }

    [Fact]
    public void MapBffEndpoints_UserEndpoint_CarriesTheMetadata()
    {
        var endpoints = MapEndpoints();

        var user = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/user");
        var login = Assert.Single(endpoints, x => x.RoutePattern.RawText == "/bff/login");

        Assert.NotNull(user.Metadata.GetMetadata<BffUserEndpointMetadata>());
        Assert.Null(login.Metadata.GetMetadata<BffUserEndpointMetadata>());
    }

    #endregion
}
