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

namespace NCode.Identity.Server.ConformanceEnvironment;

/// <summary>
/// Configures the <strong>conformance-only</strong> seeding of the confidential clients and the deterministic test
/// user that the OpenID Foundation conformance suite drives against a launched Playground host, so the suite always
/// sees the same identities with no manual API calls. Each half is inert unless its inputs are configured; this is a
/// development/conformance convenience and must not be used in production.
/// </summary>
[PublicAPI]
public sealed class ConformanceSeedOptions
{
    /// <summary>
    /// The default configuration section name bound to these options.
    /// </summary>
    public const string DefaultSectionName = "ConformanceSeed";

    /// <summary>
    /// Gets or sets the workload tenant id to seed into. When empty, every workload tenant is seeded.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the confidential clients the conformance suite exercises. The Basic OP plan needs two distinct
    /// <c>client_secret_basic</c> clients plus one <c>client_secret_post</c> client (which may be the same record if
    /// it accepts either method). Defaults to empty so the seeder stays inert unless the conformance profile supplies
    /// the registrations.
    /// </summary>
    public IList<ConformanceClientSeed> Clients { get; set; } = [];

    /// <summary>
    /// Gets or sets the deterministic test user name the suite signs in as through the interactive login page. When
    /// empty, no user is seeded.
    /// </summary>
    public string? TestUserName { get; set; } = "conformance-user";

    /// <summary>
    /// Gets or sets the test user email address.
    /// </summary>
    public string? TestUserEmail { get; set; } = "conformance-user@localhost";

    /// <summary>
    /// Gets or sets the test user password. When empty, no user is seeded (there is intentionally no default
    /// password).
    /// </summary>
    public string? TestUserPassword { get; set; }

    /// <summary>
    /// Gets a value indicating whether the deterministic test user should be seeded.
    /// </summary>
    public bool SeedTestUser =>
        !string.IsNullOrEmpty(TestUserName) && !string.IsNullOrEmpty(TestUserPassword);
}
