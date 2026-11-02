// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides an abstraction for selecting the appropriate <see cref="IOpenIdMessageFactory"/> for a given type discriminator.
/// </summary>
public interface IOpenIdMessageFactorySelector
{
    /// <summary>
    /// Gets the message factory for the specified type discriminator.
    /// </summary>
    /// <param name="typeDiscriminator">The type discriminator of the message to create.</param>
    /// <returns>The message factory for the specified type discriminator.</returns>
    IOpenIdMessageFactory GetFactory(string typeDiscriminator);
}
