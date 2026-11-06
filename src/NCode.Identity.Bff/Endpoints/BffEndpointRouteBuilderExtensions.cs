// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;

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
        /// Maps the <c>login</c>, <c>callback</c>, <c>user</c>, <c>logout</c> and <c>backchannel-logout</c> endpoints of
        /// each frontend under its <see cref="BffFrontendOptions.PathPrefix"/>, limited to that frontend, and the
        /// <c>jwks</c> endpoint of each frontend whose <see cref="BffJwksOptions.Enabled"/> is <c>true</c>.
        /// </summary>
        /// <returns>The <see cref="RouteGroupBuilder"/> of the endpoints, so that the application can add a rate
        /// limiter or exclude them from the API description.</returns>
        public RouteGroupBuilder MapBffEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var catalog = endpoints.ServiceProvider.GetRequiredService<BffFrontendCatalog>();

            var group = endpoints.MapGroup(string.Empty).AllowAnonymous().WithTags(Tag);

            foreach (var frontend in catalog.Frontends)
            {
                var frontendGroup = group
                    .MapGroup(frontend.Options.PathPrefix)
                    .RequireBffFrontend(frontend.Name);

                MapFrontendEndpoints(frontendGroup, frontend);
            }

            return group;
        }

        /// <summary>
        /// Forwards every request under a local path prefix to a remote API, with the token of the API's requirement.
        /// </summary>
        /// <remarks>
        /// The endpoint is a BFF API: each request must carry the anti-forgery header. The token requirement decides
        /// whether a session is needed; under an application's fallback authorization policy, a requirement that works
        /// without a session, such as <see cref="BffTokenRequirementNames.UserOrClient"/>, also needs
        /// <c>.AllowAnonymous()</c> on the returned builder. The endpoint serves whichever frontend a request selects;
        /// <c>.RequireBffFrontend(...)</c> on the returned builder limits it to named frontends.
        /// </remarks>
        /// <param name="path">The local path prefix, such as <c>/api/orders</c>.</param>
        /// <param name="destination">The absolute base URL that requests are forwarded to.</param>
        /// <param name="configure">Configures the remote API's token requirement, methods and timeout.</param>
        /// <returns>The convention builder of the mapped endpoint.</returns>
        /// <exception cref="InvalidOperationException">
        /// The remote API is invalid, overlaps the endpoints of a frontend or another remote API, or names a token
        /// requirement that is not registered.
        /// </exception>
        public IEndpointConventionBuilder MapBffRemoteApi(
            string path,
            string destination,
            Action<BffRemoteApiOptions>? configure = null
        )
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(destination);

            var api = new BffRemoteApiOptions { Path = path, Destination = destination };
            configure?.Invoke(api);

            return BffRemoteApiEndpoints.Map(endpoints, nameof(MapBffRemoteApi), api, null);
        }

        /// <summary>
        /// Forwards the remote APIs configured in <see cref="BffFrontendOptions.RemoteApis"/> of each frontend, each
        /// limited to the frontend that configures it.
        /// </summary>
        /// <returns>The group that holds the remote API endpoints, which conventions can be added to.</returns>
        /// <exception cref="InvalidOperationException">
        /// A configured remote API overlaps a remote API of the same frontend or one mapped in code, or names a token
        /// requirement that is not registered.
        /// </exception>
        public RouteGroupBuilder MapBffRemoteApis()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var catalog = endpoints.ServiceProvider.GetRequiredService<BffFrontendCatalog>();
            var group = endpoints.MapGroup(string.Empty);

            foreach (var frontend in catalog.Frontends)
            {
                foreach (var (name, api) in frontend.Options.RemoteApis)
                {
                    BffRemoteApiEndpoints.Map(
                        group,
                        $"Bff:Frontends:{frontend.Name}:RemoteApis:{name}",
                        api,
                        frontend
                    );
                }
            }

            return group;
        }
    }

    private static void MapFrontendEndpoints(RouteGroupBuilder group, BffFrontend frontend)
    {
        group
            .MapGet(
                "/login",
                static (HttpContext context) =>
                    context
                        .RequestServices.GetRequiredService<BffLoginHandler>()
                        .HandleAsync(context)
            )
            .WithName($"Bff.{frontend.Name}.Login");

        group
            .MapGet(
                "/callback",
                static (HttpContext context) =>
                    context
                        .RequestServices.GetRequiredService<BffCallbackHandler>()
                        .HandleAsync(context)
            )
            .WithName($"Bff.{frontend.Name}.Callback");

        group
            .MapGet(
                "/user",
                static (HttpContext context) =>
                    context
                        .RequestServices.GetRequiredService<BffUserHandler>()
                        .HandleAsync(context)
            )
            .WithName($"Bff.{frontend.Name}.User")
            .WithMetadata(BffUserEndpointMetadata.Instance);

        group
            .MapPost(
                "/logout",
                static (HttpContext context) =>
                    context
                        .RequestServices.GetRequiredService<BffLogoutHandler>()
                        .HandleAsync(context)
            )
            .WithName($"Bff.{frontend.Name}.Logout");

        group
            .MapPost(
                "/backchannel-logout",
                static (HttpContext context) =>
                    context
                        .RequestServices.GetRequiredService<BffBackChannelLogoutHandler>()
                        .HandleAsync(context)
            )
            .WithName($"Bff.{frontend.Name}.BackchannelLogout");

        if (frontend.Options.Jwks.Enabled)
        {
            group
                .MapGet(
                    "/jwks",
                    static (HttpContext context) =>
                        context
                            .RequestServices.GetRequiredService<BffJwksHandler>()
                            .HandleAsync(context)
                )
                .WithName($"Bff.{frontend.Name}.Jwks");
        }
    }
}
