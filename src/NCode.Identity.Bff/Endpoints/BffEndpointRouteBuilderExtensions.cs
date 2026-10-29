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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Provides the ability to map the backend-for-frontend (BFF) endpoints onto the HTTP request pipeline.
/// </summary>
[PublicAPI]
public static class BffEndpointRouteBuilderExtensions
{
    private const string Tag = "Bff";

    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> instance to map the endpoints to.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the <c>login</c>, <c>callback</c>, <c>user</c>, <c>logout</c> and <c>backchannel-logout</c> endpoints under the configured
        /// <see cref="BffOptions.PathPrefix"/>.
        /// </summary>
        /// <returns>The <see cref="RouteGroupBuilder"/> of the endpoints, so that the application can add a rate
        /// limiter or exclude them from the API description.</returns>
        public RouteGroupBuilder MapBffEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var options = endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>();
            var prefix = options.Value.PathPrefix;

            var group = endpoints.MapGroup(prefix).AllowAnonymous().WithTags(Tag);

            group
                .MapGet(
                    "/login",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffLoginHandler>()
                            .HandleAsync(context)
                )
                .WithName("Bff.Login");

            group
                .MapGet(
                    "/callback",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffCallbackHandler>()
                            .HandleAsync(context)
                )
                .WithName("Bff.Callback");

            group
                .MapGet(
                    "/user",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffUserHandler>()
                            .HandleAsync(context)
                )
                .WithName("Bff.User")
                .WithMetadata(BffUserEndpointMetadata.Instance);

            group
                .MapGet(
                    "/logout",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffLogoutHandler>()
                            .HandleAsync(context)
                )
                .WithName("Bff.Logout");

            group
                .MapPost(
                    "/backchannel-logout",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffBackChannelLogoutHandler>()
                            .HandleAsync(context)
                )
                .WithName("Bff.BackchannelLogout");

            return group;
        }
    }
}
