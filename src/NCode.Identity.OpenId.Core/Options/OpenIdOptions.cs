// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Principals;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options used to configure OpenID.
/// </summary>
[PublicAPI]
public sealed class OpenIdOptions
{
    /// <summary>
    /// Gets the default name of the configuration section.
    /// </summary>
    public const string DefaultSectionName = "OpenId";

    /// <summary>
    /// Gets or sets the options that configure the platform, the root of the settings cascade: its configured settings
    /// and how often its persisted settings are read again.
    /// </summary>
    public OpenIdPlatformOptions Platform { get; set; } = new();

    /// <summary>
    /// Gets or sets the configurable options for tenant features.
    /// </summary>
    public OpenIdTenantOptions Tenant { get; set; } = new();

    /// <summary>
    /// Gets or sets the configurable options for team features.
    /// </summary>
    public OpenIdTeamOptions Team { get; set; } = new();

    /// <summary>
    /// Gets or sets a delegate that extracts a <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
    /// </summary>
    public GetSubjectIdentityDelegate GetSubjectIdentity { get; set; } =
        DefaultSubjectDelegates.GetSubjectIdentity;

    /// <summary>
    /// Gets or sets a delegate that extracts the subject id from a <see cref="ClaimsPrincipal"/> given an ordered list
    /// of candidate claim types. The default implementation returns the value of the first non-empty matching claim,
    /// honoring the order of the supplied claim types. The claim types are supplied per call: the tenant's
    /// <c>subject_claim_types</c> setting for an end-user, or
    /// <see cref="OpenIdConstants.DefaultSubjectClaimTypes"/> for an app-level caller.
    /// </summary>
    public GetSubjectIdDelegate GetSubjectId { get; set; } = DefaultSubjectDelegates.GetSubjectId;

    /// <summary>
    /// Gets or sets a delegate that rewrites the subject claims of a <see cref="ClaimsPrincipal"/> so its authoritative
    /// subject identifier becomes the supplied value. This is the inverse of <see cref="GetSubjectId"/>; the default
    /// implementation strips every subject-type claim and stamps a single claim of the primary subject claim type. The
    /// claim types are supplied per call: the tenant's <c>subject_claim_types</c> setting for an end-user, or
    /// <see cref="OpenIdConstants.DefaultSubjectClaimTypes"/> for an app-level caller.
    /// </summary>
    public NormalizeSubjectIdDelegate NormalizeSubjectId { get; set; } =
        DefaultSubjectDelegates.NormalizeSubjectId;

    /// <summary>
    /// Gets a list of delegates that can configure the <see cref="JsonSerializerOptions"/> used by OpenID.
    /// </summary>
    public List<Action<JsonSerializerOptions>> JsonOptionsConfigurators { get; } = [];
}
