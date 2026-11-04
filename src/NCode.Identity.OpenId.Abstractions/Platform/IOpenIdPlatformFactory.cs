// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Factory for creating a new <see cref="OpenIdPlatform"/> instance.
/// </summary>
public interface IOpenIdPlatformFactory
{
    /// <summary>
    /// Factory method to create a new <see cref="OpenIdPlatform"/> instance.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdPlatform"/> instance.</returns>
    ValueTask<OpenIdPlatform> CreateAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    );
}
