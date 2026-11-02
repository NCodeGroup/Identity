// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server;

/// <summary>
/// Controls what <c>MapIdentityServerApiReference</c> maps. The OpenAPI document (the spec JSON) and the API reference
/// UI are independent toggles: a host may expose the machine-readable spec without the UI, serve the UI from a document
/// hosted elsewhere, or disable both in environments where neither should be reachable.
/// </summary>
[PublicAPI]
public sealed class IdentityServerApiReferenceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the OpenAPI document (the spec JSON) endpoint is mapped.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool MapOpenApiDocument { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Scalar API reference UI is mapped. The UI fetches the OpenAPI
    /// document at runtime, so it must be reachable (either mapped here or served elsewhere) for the UI to load.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool MapApiReferenceUi { get; set; } = true;
}
