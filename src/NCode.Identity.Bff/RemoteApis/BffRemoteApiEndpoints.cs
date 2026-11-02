// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Maps remote APIs as BFF API endpoints, after checking each one as the options validator does.
/// </summary>
internal static class BffRemoteApiEndpoints
{
    private const string RemainderPattern = "/{**" + BffRemoteApiPaths.RemainderParameter + "}";

    /// <summary>
    /// Maps one remote API.
    /// </summary>
    /// <param name="endpoints">The route builder to map the remote API on.</param>
    /// <param name="key">The name that failure messages use for the remote API.</param>
    /// <param name="api">The options of the remote API.</param>
    /// <returns>The convention builder of the mapped endpoint.</returns>
    /// <exception cref="InvalidOperationException">The options are invalid, or the path overlaps another.</exception>
    public static IEndpointConventionBuilder Map(
        IEndpointRouteBuilder endpoints,
        string key,
        BffRemoteApiOptions api
    )
    {
        var services = endpoints.ServiceProvider;
        var environment = services.GetRequiredService<IHostEnvironment>();
        var bffOptions = services.GetRequiredService<IOptions<BffOptions>>().Value;

        List<string> failures = [];
        BffRemoteApiValidation.Validate(key, api, environment.IsDevelopment(), failures);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        if (
            !string.IsNullOrEmpty(bffOptions.PathPrefix)
            && BffRemoteApiValidation.Overlaps(api.Path, bffOptions.PathPrefix)
        )
        {
            throw new InvalidOperationException(
                $"{key}:Path '{api.Path}' must not overlap Bff:PathPrefix '{bffOptions.PathPrefix}'."
            );
        }

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

        return endpoints
            .Map(api.Path + RemainderPattern, context => handler.HandleAsync(context, remoteApi))
            .WithDisplayName($"BFF remote API {api.Path}")
            .AsBffApi();
    }
}
