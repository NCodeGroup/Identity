// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server;

/// <summary>
/// Binds the <c>ApiReference</c> configuration section that controls what <c>MapIdentityServerApiReference</c> maps.
/// The OpenAPI document is served at <c>/openapi/v1.json</c> and the Scalar API reference page at
/// <see cref="PagePath"/>. Both are off unless configured. The page renders the document this host serves, so it requires
/// <see cref="DocumentEnabled"/>.
/// </summary>
[PublicAPI]
public sealed class IdentityServerApiReferenceOptions
{
    /// <summary>
    /// The default configuration section name, <c>ApiReference</c>.
    /// </summary>
    public const string DefaultSectionName = "ApiReference";

    /// <summary>
    /// The host-root route prefix under which the Scalar API reference page is mapped.
    /// </summary>
    public const string PagePrefix = "/scalar";

    /// <summary>
    /// The host-root path at which the Scalar API reference page is served, <c>/scalar/v1</c>.
    /// </summary>
    public const string PagePath = PagePrefix + "/v1";

    /// <summary>
    /// Gets or sets a value indicating whether the OpenAPI document is served at <c>/openapi/v1.json</c>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DocumentEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Scalar API reference page is served at <see cref="PagePath"/>.
    /// The page renders the document this host serves, so it requires <see cref="DocumentEnabled"/>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool PageEnabled { get; set; }
}
