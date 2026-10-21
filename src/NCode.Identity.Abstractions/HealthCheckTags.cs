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

namespace NCode.Identity;

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
