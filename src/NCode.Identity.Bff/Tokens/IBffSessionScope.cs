// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The session that a token is obtained for: the session of a request, or the session that a connection holds.
/// </summary>
internal interface IBffSessionScope : IBffTokenContext
{
    /// <summary>
    /// Gets the frontend that serves the scope, or <see langword="null"/> when no frontend serves it.
    /// </summary>
    BffFrontend? SelectedFrontend { get; }

    /// <summary>
    /// Resolves the session of the scope, or returns <see langword="null"/> when the scope has none.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the operation.</param>
    ValueTask<BffSessionState?> ResolveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called when the session of the scope has ended: a request deletes its session cookie, and a connection cancels
    /// its <see cref="IBffConnectionSession.SessionEnded"/> token.
    /// </summary>
    void OnSessionEnded();

    /// <summary>
    /// Returns the session scope of a token context that the BFF created.
    /// </summary>
    /// <param name="context">The token context.</param>
    /// <exception cref="InvalidOperationException">The BFF did not create the token context.</exception>
    static IBffSessionScope From(IBffTokenContext context) =>
        context as IBffSessionScope
        ?? throw new InvalidOperationException("The token context was not created by the BFF.");
}
