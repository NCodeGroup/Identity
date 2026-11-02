// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Factory for creating a new <see cref="OpenIdEnvironment"/> instance.
/// </summary>
public interface IOpenIdEnvironmentFactory
{
    /// <summary>
    /// Factory method to create a new <see cref="OpenIdEnvironment"/> instance.
    /// </summary>
    /// <returns>The newly created <see cref="OpenIdEnvironment"/> instance.</returns>
    OpenIdEnvironment Create();
}
