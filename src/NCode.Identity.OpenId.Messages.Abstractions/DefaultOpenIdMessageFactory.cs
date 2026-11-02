// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdMessageFactory"/> abstraction.
/// </summary>
/// <typeparam name="T">The type of the <see cref="OpenIdMessage"/> to create.</typeparam>
internal class DefaultOpenIdMessageFactory<T> : IOpenIdMessageFactory
    where T : OpenIdMessage, new()
{
    /// <inheritdoc />
    public string TypeDiscriminator => typeof(T).Name;

    /// <inheritdoc />
    public IOpenIdMessage Create(
        OpenIdEnvironment openIdEnvironment,
        IEnumerable<IParameter> parameters
    )
    {
        var message = new T();
        message.Initialize(openIdEnvironment, parameters);
        return message;
    }
}
