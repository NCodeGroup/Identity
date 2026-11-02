// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdContextFeature"/> abstraction.
/// </summary>
internal class DefaultOpenIdContextFeature : IOpenIdContextFeature
{
    /// <inheritdoc />
    public required OpenIdContext OpenIdContext { get; init; }
}
