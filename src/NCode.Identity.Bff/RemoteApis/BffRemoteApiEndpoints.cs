// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Maps remote APIs as BFF API endpoints, after checking each one as the options validator does.
/// </summary>
internal static class BffRemoteApiEndpoints
{
    private const string RemainderPattern = "/{**" + BffRemoteApiPaths.RemainderParameter + "}";

    /// <summary>
    /// Maps one remote API that is mapped in code, which serves whichever frontend a request selects.
    /// </summary>
    /// <param name="endpoints">The route builder to map the remote API on.</param>
    /// <param name="key">The name that failure messages use for the remote API.</param>
    /// <param name="api">The options of the remote API.</param>
    /// <returns>The convention builder of the mapped endpoint.</returns>
    /// <exception cref="InvalidOperationException">
    /// The options are invalid, the path overlaps another remote API mapped in code or the endpoints or configured
    /// remote APIs of a frontend, or the token requirement is not registered.
    /// </exception>
    public static IEndpointConventionBuilder MapInCode(
        IEndpointRouteBuilder endpoints,
        string key,
        BffRemoteApiOptions api
    )
    {
        var services = endpoints.ServiceProvider;
        var localDevelopment = services.GetRequiredService<LocalDevelopmentMode>();
        var catalog = services.GetRequiredService<BffFrontendCatalog>();

        List<string> failures = [];
        BffRemoteApiValidation.Validate(key, api, localDevelopment.Enabled, failures);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        var isRegistered = services
            .GetServices<IBffTokenRequirement>()
            .Any(requirement =>
                string.Equals(requirement.Name, api.TokenRequirement, StringComparison.Ordinal)
            );
        if (!isRegistered)
        {
            throw new InvalidOperationException(
                $"{key}:TokenRequirement '{api.TokenRequirement}' is not a registered token requirement."
            );
        }

        var registry = services.GetRequiredService<BffRemoteApiRegistry>();
        if (!registry.TryRegister(api.Path, out var overlapping))
        {
            throw new InvalidOperationException(
                $"{key}:Path '{api.Path}' must not overlap the remote API mapped at '{overlapping}'."
            );
        }

        catalog.AddCheck(new BffRemoteApiPathCheck(key, api.Path));

        return MapEndpoint(endpoints, api, null);
    }

    /// <summary>
    /// Maps the endpoint of one remote API.
    /// </summary>
    /// <param name="endpoints">The route builder to map the remote API on.</param>
    /// <param name="api">The options of the remote API.</param>
    /// <param name="frontend">
    /// The frontend that the remote API belongs to, or <c>null</c> for a remote API that serves whichever frontend a
    /// request selects.
    /// </param>
    /// <returns>The convention builder of the mapped endpoint.</returns>
    public static IEndpointConventionBuilder MapEndpoint(
        IEndpointRouteBuilder endpoints,
        BffRemoteApiOptions api,
        BffFrontend? frontend
    )
    {
        var services = endpoints.ServiceProvider;

        var tokenRequest = new BffTokenRequest(api.TokenRequirement)
        {
            Resource = api.Resource,
            Scope = api.Scope,
        };
        var invoker = services
            .GetRequiredService<BffRemoteApiInvokerFactory>()
            .CreateInvoker(tokenRequest);
        var remoteApi = new BffRemoteApi(
            api.Path,
            api.Destination,
            tokenRequest,
            api.AllowedMethods,
            api.Timeout,
            invoker
        );
        var handler = services.GetRequiredService<BffRemoteApiHandler>();

        var builder = endpoints
            .Map(
                api.Path + RemainderPattern,
                context => handler.HandleAsync(context, remoteApi, context.RequestAborted).AsTask()
            )
            .WithDisplayName(
                frontend is null
                    ? $"BFF remote API {api.Path}"
                    : $"BFF remote API {api.Path} ({frontend.Name})"
            )
            .AsBffApi();

        return frontend is null ? builder : builder.RequireBffFrontend(frontend.Name);
    }
}
