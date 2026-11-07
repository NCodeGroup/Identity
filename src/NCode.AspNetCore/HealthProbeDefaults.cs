// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.AspNetCore;

/// <summary>
/// Contains the paths and endpoint names of the liveness and readiness probes that
/// <see cref="HealthProbeEndpointRouteBuilderExtensions.MapHealthProbes"/> maps.
/// </summary>
[PublicAPI]
public static class HealthProbeDefaults
{
    /// <summary>
    /// The path of the liveness probe.
    /// </summary>
    public const string LivenessPath = "/health/live";

    /// <summary>
    /// The path of the readiness probe.
    /// </summary>
    public const string ReadinessPath = "/health/ready";

    /// <summary>
    /// The endpoint name of the liveness probe.
    /// </summary>
    public const string LivenessEndpointName = "health_live_endpoint";

    /// <summary>
    /// The endpoint name of the readiness probe.
    /// </summary>
    public const string ReadinessEndpointName = "health_ready_endpoint";
}
