// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Resolves a <c>response_mode</c> to the registered <see cref="IAuthorizationResponseModeHandler"/> that delivers it.
/// </summary>
// See ADR-1201.
internal interface IAuthorizationResponseModeProvider
{
    /// <summary>
    /// Resolves the requested response mode, or the response type's default when none is requested, to its registered
    /// handler. <c>jwt</c> resolves to the handler whose <see cref="IAuthorizationResponseModeHandler.SecuredResponseMode"/>
    /// is that default (JARM §2.3.4). Returns <see langword="false"/> when no registered handler serves it.
    /// </summary>
    /// <param name="requestedResponseMode">The requested <c>response_mode</c>, or <see langword="null"/> when none is requested.</param>
    /// <param name="responseTypes">The names of the response type, in any order.</param>
    /// <param name="handler">The handler that delivers the response mode, when one is registered.</param>
    /// <returns><see langword="true"/> when a registered handler serves the response mode.</returns>
    // See ADR-1201.
    bool TryResolve(
        string? requestedResponseMode,
        IReadOnlyCollection<string> responseTypes,
        [NotNullWhen(true)] out IAuthorizationResponseModeHandler? handler
    );
}
