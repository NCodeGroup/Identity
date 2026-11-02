// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the parameters for an <c>OAuth</c> or <c>OpenID Connect</c> authorization request from the request
/// object only.
/// </summary>
[PublicAPI]
public interface IAuthorizationRequestObject
    : IOpenIdMessage,
        IBaseAuthorizationRequestValues,
        ISupportClone<IAuthorizationRequestObject>
{
    /// <summary>
    /// Gets or sets a value indicating where the <c>request object</c> was loaded from.
    /// </summary>
    RequestObjectSource RequestObjectSource { get; set; }
}
