// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using NCode.Registration;

namespace NCode.AspNetCore;

/// <summary>
/// Maps the liveness and readiness probe endpoints of a host. The liveness probe runs no health check, so a
/// dependency outage never restarts a replica. The readiness probe runs the health checks tagged with
/// <see cref="HealthCheckTags.Readiness"/>.
/// </summary>
// See ADR-1456.
[PublicAPI]
public static class HealthProbeEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the liveness probe at <see cref="HealthProbeDefaults.LivenessPath"/> and the readiness probe at
        /// <see cref="HealthProbeDefaults.ReadinessPath"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="RouteGroupBuilder"/> that holds both probes, so the caller can apply a convention, such as
        /// <c>RequireHost</c>, to both.
        /// </returns>
        public RouteGroupBuilder MapHealthProbes()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var group = endpoints.MapGroup(string.Empty);

            group
                .MapHealthChecks(
                    HealthProbeDefaults.LivenessPath,
                    new HealthCheckOptions { Predicate = static _ => false }
                )
                .WithName(HealthProbeDefaults.LivenessEndpointName);

            group
                .MapHealthChecks(
                    HealthProbeDefaults.ReadinessPath,
                    new HealthCheckOptions
                    {
                        Predicate = static check => check.Tags.Contains(HealthCheckTags.Readiness),
                    }
                )
                .WithName(HealthProbeDefaults.ReadinessEndpointName);

            return group;
        }
    }
}
