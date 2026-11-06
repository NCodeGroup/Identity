// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Decides whether an existing session may continue to be used.
/// </summary>
/// <remarks>
/// Validators run when a request resolves a session and when a hub or Blazor circuit connection revalidates the
/// session it holds. For a connection, <see cref="BffSessionValidationContext.HttpContext"/> is
/// <see langword="null"/>, so a validator reads the request only after checking for it. A validator can only end a
/// session; every registered validator must keep it.
/// </remarks>
[PublicAPI]
public interface IBffSessionValidator
{
    /// <summary>
    /// Decides whether the session may continue.
    /// </summary>
    /// <param name="context">The context of the session to validate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> to keep the session; otherwise, <see langword="false"/> to end it as
    /// <c>ended_by_validator</c>.
    /// </returns>
    ValueTask<bool> ValidateAsync(
        BffSessionValidationContext context,
        CancellationToken cancellationToken
    );
}
