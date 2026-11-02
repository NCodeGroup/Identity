// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Errors;

/// <summary>
/// Provides the ability to create an <see cref="IOpenIdError"/> instance that represents an <c>OAuth</c> or <c>OpenID Connect</c> error.
/// </summary>
[PublicAPI]
public interface IOpenIdErrorFactory
{
    /// <summary>
    /// Creates an <see cref="IOpenIdError"/> instance for an <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// </summary>
    /// <param name="errorCode">The <c>error</c> parameter for the <c>OAuth</c> or <c>OpenID Connect</c> error.</param>
    /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
    IOpenIdError Create(string errorCode);
}
