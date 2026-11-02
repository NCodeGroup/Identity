// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Allows for implementations of <see cref="IOpenIdMessage"/> to specify a type discriminator value.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class OpenIdTypeDiscriminatorAttribute(string typeDiscriminator) : Attribute
{
    /// <summary>
    /// Gets the type discriminator value.
    /// </summary>
    public string TypeDiscriminator { get; } = typeDiscriminator;
}
