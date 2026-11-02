// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides access to the current <see cref="OpenIdEnvironment"/> instance.
/// </summary>
public interface IOpenIdEnvironmentProvider
{
    /// <summary>
    /// Gets the current <see cref="OpenIdEnvironment"/> instance.
    /// </summary>
    /// <returns>The current <see cref="OpenIdEnvironment"/> instance.</returns>
    OpenIdEnvironment Get();
}
