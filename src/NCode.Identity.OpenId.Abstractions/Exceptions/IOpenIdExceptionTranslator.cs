// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a strategy that maps an exception that a package owns to the protocol error that an <c>OAuth</c> or
/// <c>OpenID Connect</c> endpoint answers it with.
/// </summary>
/// <remarks>
/// Implementations are registered as a collection with <c>TryAddEnumerable</c>, and the first translator that answers
/// wins. An exception that no translator answers is reported as <c>server_error</c>.
/// </remarks>
// See ADR-1428.
[PublicAPI]
public interface IOpenIdExceptionTranslator
{
    /// <summary>
    /// Attempts to map an exception to the <see cref="IOpenIdError"/> that an <c>OAuth</c> or <c>OpenID Connect</c>
    /// endpoint answers it with.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="error">When this method returns <c>true</c>, the <see cref="IOpenIdError"/> that represents the exception.</param>
    /// <returns><c>true</c> if this translator owns the exception and produced an error; otherwise, <c>false</c>.</returns>
    bool TryTranslate(
        OpenIdEnvironment openIdEnvironment,
        Exception exception,
        [NotNullWhen(true)] out IOpenIdError? error
    );
}
