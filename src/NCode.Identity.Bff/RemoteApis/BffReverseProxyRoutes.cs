// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Yarp.ReverseProxy.Configuration;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Reads and checks the BFF metadata of a YARP route.
/// </summary>
internal static class BffReverseProxyRoutes
{
    private static string[] OptionalKeys { get; } =
    [BffReverseProxyMetadataKeys.Resource, BffReverseProxyMetadataKeys.Scope];

    /// <summary>
    /// Determines whether the route's metadata names a token requirement, which makes the route a BFF remote API.
    /// </summary>
    /// <param name="route">The route to read.</param>
    public static bool IsRemoteApi(RouteConfig route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.Metadata?.ContainsKey(BffReverseProxyMetadataKeys.TokenRequirement) == true;
    }

    /// <summary>
    /// Gets the token request that the route's metadata names, or <see langword="null"/> when the route is not a BFF
    /// remote API.
    /// </summary>
    /// <param name="route">The route to read, whose metadata <see cref="Validate"/> accepted.</param>
    public static BffTokenRequest? GetTokenRequest(RouteConfig route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var metadata = route.Metadata;
        if (
            metadata is null
            || !metadata.TryGetValue(
                BffReverseProxyMetadataKeys.TokenRequirement,
                out var requirement
            )
        )
        {
            return null;
        }

        return new BffTokenRequest(requirement)
        {
            Resource = metadata.GetValueOrDefault(BffReverseProxyMetadataKeys.Resource),
            Scope = metadata.GetValueOrDefault(BffReverseProxyMetadataKeys.Scope),
        };
    }

    /// <summary>
    /// Gets the name of the frontend that the route's metadata names, or <see langword="null"/> when the route serves
    /// every frontend.
    /// </summary>
    /// <param name="route">The route to read, whose metadata <see cref="Validate"/> accepted.</param>
    public static string? GetFrontend(RouteConfig route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route.Metadata?.GetValueOrDefault(BffReverseProxyMetadataKeys.Frontend);
    }

    /// <summary>
    /// Checks the route's BFF metadata: a token requirement must be registered, a resource or a scope needs a token
    /// requirement and must not be blank, and a frontend must be configured.
    /// </summary>
    /// <param name="route">The route to check.</param>
    /// <param name="requirements">The names of the registered token requirements.</param>
    /// <param name="frontends">The names of the configured frontends.</param>
    /// <param name="errors">The list that each failure is added to.</param>
    public static void Validate(
        RouteConfig route,
        IReadOnlySet<string> requirements,
        IReadOnlySet<string> frontends,
        ICollection<Exception> errors
    )
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(errors);

        var metadata = route.Metadata;
        if (metadata is null)
            return;

        if (metadata.TryGetValue(BffReverseProxyMetadataKeys.Frontend, out var frontend))
            ValidateFrontend(route, frontend, frontends, errors);

        var hasRequirement = metadata.TryGetValue(
            BffReverseProxyMetadataKeys.TokenRequirement,
            out var requirement
        );
        if (hasRequirement)
            ValidateRequirement(route, requirement, requirements, errors);

        foreach (var key in OptionalKeys)
        {
            if (!metadata.TryGetValue(key, out var value))
                continue;

            if (!hasRequirement)
            {
                errors.Add(
                    Failure(route, $"{key} needs {BffReverseProxyMetadataKeys.TokenRequirement}.")
                );
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(Failure(route, $"{key} must not be blank."));
            }
        }
    }

    private static void ValidateFrontend(
        RouteConfig route,
        string? frontend,
        IReadOnlySet<string> frontends,
        ICollection<Exception> errors
    )
    {
        if (string.IsNullOrWhiteSpace(frontend))
        {
            errors.Add(
                Failure(route, $"{BffReverseProxyMetadataKeys.Frontend} must not be blank.")
            );
        }
        else if (!frontends.Contains(frontend))
        {
            errors.Add(
                Failure(
                    route,
                    $"{BffReverseProxyMetadataKeys.Frontend} '{frontend}' is not a configured BFF frontend."
                )
            );
        }
    }

    private static void ValidateRequirement(
        RouteConfig route,
        string? requirement,
        IReadOnlySet<string> requirements,
        ICollection<Exception> errors
    )
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            errors.Add(
                Failure(route, $"{BffReverseProxyMetadataKeys.TokenRequirement} must not be blank.")
            );
        }
        else if (!requirements.Contains(requirement))
        {
            errors.Add(
                Failure(
                    route,
                    $"{BffReverseProxyMetadataKeys.TokenRequirement} '{requirement}' is not a registered token requirement."
                )
            );
        }
    }

    private static ArgumentException Failure(RouteConfig route, string message) =>
        new($"The metadata of route '{route.RouteId}' is invalid: {message}");
}
