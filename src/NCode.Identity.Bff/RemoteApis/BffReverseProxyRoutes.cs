// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
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
    /// requirement and must not be blank, and a frontend must be a valid frontend name; whether the frontends
    /// configure it is the endpoint check's to say, because YARP and the BFF apply one configuration change
    /// separately (ADR-1442).
    /// </summary>
    /// <param name="route">The route to check.</param>
    /// <param name="requirements">The names of the registered token requirements.</param>
    /// <param name="errors">The list that each failure is added to.</param>
    public static void Validate(
        RouteConfig route,
        IReadOnlySet<string> requirements,
        ICollection<Exception> errors
    )
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(errors);

        var metadata = route.Metadata;
        if (metadata is null)
            return;

        if (
            metadata.TryGetValue(BffReverseProxyMetadataKeys.Frontend, out var frontend)
            && CheckFrontend(route, frontend) is { } frontendFailure
        )
        {
            errors.Add(frontendFailure);
        }

        var hasRequirement = metadata.TryGetValue(
            BffReverseProxyMetadataKeys.TokenRequirement,
            out var requirement
        );
        if (
            hasRequirement
            && CheckRequirement(route, requirement, requirements) is { } requirementFailure
        )
        {
            errors.Add(requirementFailure);
        }

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

    private static ArgumentException? CheckFrontend(RouteConfig route, string? frontend)
    {
        if (string.IsNullOrWhiteSpace(frontend))
        {
            return Failure(route, $"{BffReverseProxyMetadataKeys.Frontend} must not be blank.");
        }

        if (!BffFrontendNames.IsValid(frontend))
        {
            return Failure(
                route,
                $"{BffReverseProxyMetadataKeys.Frontend} '{frontend}' is not a valid BFF frontend name."
            );
        }

        return null;
    }

    private static ArgumentException? CheckRequirement(
        RouteConfig route,
        string? requirement,
        IReadOnlySet<string> requirements
    )
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return Failure(
                route,
                $"{BffReverseProxyMetadataKeys.TokenRequirement} must not be blank."
            );
        }

        if (!requirements.Contains(requirement))
        {
            return Failure(
                route,
                $"{BffReverseProxyMetadataKeys.TokenRequirement} '{requirement}' is not a registered token requirement."
            );
        }

        return null;
    }

    private static ArgumentException Failure(RouteConfig route, string message) =>
        new($"The metadata of route '{route.RouteId}' is invalid: {message}");
}
