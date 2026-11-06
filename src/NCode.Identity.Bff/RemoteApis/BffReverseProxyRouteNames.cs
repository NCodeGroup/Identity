// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Provides the names that a YARP route's BFF metadata may refer to: the registered token requirements and the
/// configured frontends.
/// </summary>
/// <param name="requirements">The names of the registered token requirements.</param>
/// <param name="frontends">The names of the configured frontends.</param>
internal sealed class BffReverseProxyRouteNames(
    IReadOnlySet<string> requirements,
    IReadOnlySet<string> frontends
)
{
    /// <summary>
    /// Gets the names of the registered token requirements.
    /// </summary>
    public IReadOnlySet<string> Requirements { get; } = requirements;

    /// <summary>
    /// Gets the names of the configured frontends.
    /// </summary>
    public IReadOnlySet<string> Frontends { get; } = frontends;
}
