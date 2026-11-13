// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Issues and verifies the one-time setup token that guards the creation of the root administrator. Register a custom
/// implementation to replace or extend the default behavior.
/// </summary>
[PublicAPI]
public interface ISetupTokenService
{
    /// <summary>
    /// Gets the source of the setup token, one of the <see cref="SetupTokenSources"/> values.
    /// </summary>
    string TokenSource { get; }

    /// <summary>
    /// Ensures that a setup token is available for an uninitialized instance, generating and storing one when the
    /// token is not configured.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when a token is available.</returns>
    ValueTask EnsureTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the supplied token is the current setup token.
    /// </summary>
    /// <param name="token">The token to verify.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the token is the current, unexpired setup token; otherwise, <c>false</c>.</returns>
    ValueTask<bool> VerifyAsync(string token, CancellationToken cancellationToken);
}
