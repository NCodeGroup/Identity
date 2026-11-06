// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Provides the ability to read the frontend of an <see cref="IBffSessionScope"/>.
/// </summary>
internal static class BffSessionScopeExtensions
{
    /// <param name="scope">The session scope to read.</param>
    extension(IBffSessionScope scope)
    {
        /// <summary>
        /// Returns the frontend that serves the scope.
        /// </summary>
        /// <returns>The frontend that serves the scope.</returns>
        /// <exception cref="InvalidOperationException">No frontend serves the scope.</exception>
        public BffFrontend RequireFrontend()
        {
            ArgumentNullException.ThrowIfNull(scope);

            return scope.SelectedFrontend
                ?? throw new InvalidOperationException("The token context has no frontend.");
        }
    }
}
