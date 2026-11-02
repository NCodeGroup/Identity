// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization request from the request
/// message only.
/// </summary>
[PublicAPI]
public interface IAuthorizationRequestMessage
    : IOpenIdMessage,
        IBaseAuthorizationRequestValues,
        ISupportClone<IAuthorizationRequestMessage>
{
    /// <summary>
    /// Gets or sets the <c>request</c> parameter.
    /// </summary>
    string? RequestJwt { get; set; }

    /// <summary>
    /// Gets or sets the <c>request_uri</c> parameter.
    /// </summary>
    Uri? RequestUri { get; set; }
}
