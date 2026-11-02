// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides the <see cref="OpenIdContext"/> for the current HTTP request.
/// </summary>
[PublicAPI]
public interface IOpenIdContextFeature
{
    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> for the current HTTP request.
    /// </summary>
    OpenIdContext OpenIdContext { get; }
}
