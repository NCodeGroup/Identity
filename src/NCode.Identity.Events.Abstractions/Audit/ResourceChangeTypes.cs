// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Defines the well-known change-type discriminators that describe the lifecycle transition an
/// <see cref="IResourceChangeAuditEvent"/> represents.
/// </summary>
[PublicAPI]
public static class ResourceChangeTypes
{
    /// <summary>
    /// The resource was created.
    /// </summary>
    public const string Created = "created";

    /// <summary>
    /// The resource's metadata was updated.
    /// </summary>
    public const string Updated = "updated";

    /// <summary>
    /// The resource was deleted.
    /// </summary>
    public const string Deleted = "deleted";

    /// <summary>
    /// The resource's settings were updated.
    /// </summary>
    public const string SettingsUpdated = "settings.updated";
}
