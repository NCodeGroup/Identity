// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides an abstraction for creating <see cref="IOpenIdMessage"/> instances.
/// </summary>
[PublicAPI]
public interface IOpenIdMessageFactory
{
    /// <summary>
    /// Gets the type discriminator for the message to create.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="IOpenIdMessage"/> with the specified parameters.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="parameters">The parameters to initialize the message with.</param>
    /// <returns>The new instance of the <see cref="IOpenIdMessage"/>.</returns>
    IOpenIdMessage Create(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters);
}
