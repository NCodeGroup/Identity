// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// The base interface for all <c>OAuth</c> or <c>OpenID Connect</c> authorization request messages.
/// </summary>
[PublicAPI]
public interface IBaseAuthorizationRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets a value indicating where the request values were loaded from.
    /// </summary>
    string? AuthorizationSourceType { get; }
}
