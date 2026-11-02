// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Represents the base interface for all <c>OAuth</c> or <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public interface IBaseOpenIdMessage
{
    /// <summary>
    /// Gets the <see cref="OpenIdEnvironment"/> for the current instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    OpenIdEnvironment OpenIdEnvironment { get; }

    /// <summary>
    /// Gets the collection of strong-typed <c>OAuth</c> or <c>OpenID Connect</c> parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    IParameterCollection Parameters { get; }
}
