// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
                .MapPost(
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
