// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that seeds the developer clients of the host's Development overlay, so a test
/// can exercise them the way a local host serves them.
/// </summary>
public sealed class DeveloperSeedingHostApplicationFactory : HostApplicationFactory
{
    /// <inheritdoc />
    protected override bool SeedsDeveloperClients => true;
}
