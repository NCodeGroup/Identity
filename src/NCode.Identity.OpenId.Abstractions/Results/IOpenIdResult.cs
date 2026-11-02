// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// A non-generic accessor for an <see cref="IResult"/> that renders an <see cref="IOpenIdResponse"/>,
/// allowing the wrapped response to be inspected without knowing its concrete type. Whether the response
/// represents an error is a property of the <see cref="Response"/> value (it is an
/// <see cref="IOpenIdError"/>), not of this type.
/// </summary>
[PublicAPI]
public interface IOpenIdResult : IResult
{
    /// <summary>
    /// Gets the <see cref="IOpenIdResponse"/> that will be rendered.
    /// </summary>
    IOpenIdResponse Response { get; }
}
