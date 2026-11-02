// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Configures the opt-in interactive login/consent surface served by the OpenID Provider host. Bound from the
/// <c>InteractiveAuthentication</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class InteractiveAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the file-system path to the built single-page login/consent bundle (the <c>apps/auth</c> surface),
    /// absolute or relative to the host content root. The login and consent pages serve this bundle's
    /// <c>index.html</c> and its hashed assets. When unset, or when the bundle has not been built, the pages return
    /// guidance to build the bundle; the credential endpoints still function.
    /// </summary>
    public string? BundleRootPath { get; set; }
}
