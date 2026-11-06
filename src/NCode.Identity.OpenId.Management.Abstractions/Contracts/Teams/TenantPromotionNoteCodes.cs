// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Defines the known values of <see cref="TenantPromotionNoteResource.Code"/>.
/// </summary>
// See ADR-1425.
[PublicAPI]
public static class TenantPromotionNoteCodes
{
    /// <summary>
    /// The promoted client authenticates with a secret or key, and the target tenant's client holds none, because each
    /// environment holds its own credentials.
    /// </summary>
    public const string CredentialRequired = "credential_required";

    /// <summary>
    /// The source resource sets an environment-bound setting that the promoted document lacks, so the operator
    /// supplies the target's value.
    /// </summary>
    public const string EnvironmentSettingUnset = "environment_setting_unset";

    /// <summary>
    /// The host configures the client in the source tenant, so the promotion leaves it and its grants.
    /// </summary>
    public const string HostManaged = "host_managed";
}
