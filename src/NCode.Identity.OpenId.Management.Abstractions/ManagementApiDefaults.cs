// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides the constants of the management API's surface.
/// </summary>
[PublicAPI]
public static class ManagementApiDefaults
{
    /// <summary>
    /// The path every management route starts with, at the host root.
    /// </summary>
    public const string RoutePrefix = "/api";
}
