// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server;

/// <summary>
/// Binds the <c>ApiReference</c> configuration section that controls what <c>MapIdentityServerApiReference</c> maps.
/// The OpenAPI document is served at <c>/openapi/v1.json</c> and the Scalar API reference page at <c>/scalar/v1</c>.
/// Both are off unless configured. The page renders the document this host serves, so it requires
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
    /// Gets or sets a value indicating whether the OpenAPI document is served at <c>/openapi/v1.json</c>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DocumentEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Scalar API reference page is served at <c>/scalar/v1</c>.
    /// The page renders the document this host serves, so it requires <see cref="DocumentEnabled"/>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool PageEnabled { get; set; }
}
