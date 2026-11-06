// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures how the BFF decides that a request belongs to a frontend. The configuration section is
/// <c>Bff:Frontends:{name}:Selection</c>.
/// </summary>
[PublicAPI]
public sealed class BffFrontendSelectionOptions
{
    /// <summary>
    /// Gets or sets the name of the registered selection rule, such as <c>host</c> or <c>path</c>. A frontend without a
    /// rule serves every request that no other frontend selects. The configuration key is
    /// <c>Bff:Frontends:{name}:Selection:Rule</c>.
    /// </summary>
    public string? Rule { get; set; }

    /// <summary>
    /// Gets or sets the values that the selection rule matches a request against. The configuration key is
    /// <c>Bff:Frontends:{name}:Selection:Values</c>.
    /// </summary>
    public List<string> Values { get; set; } = [];
}
