// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Registration;

/// <summary>
/// Contains the tags that group the health checks a host maps to its probe endpoints.
/// </summary>
[PublicAPI]
public static class HealthCheckTags
{
    /// <summary>
    /// The tag of every health check that decides whether a replica is ready to serve requests. A package that
    /// registers a check a replica cannot serve without tags it with this value, and the host's readiness endpoint
    /// runs every check that carries it.
    /// </summary>
    public const string Readiness = "ready";
}
