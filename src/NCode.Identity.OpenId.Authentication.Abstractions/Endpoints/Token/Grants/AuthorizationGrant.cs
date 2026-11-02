// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents a token authorization grant.
/// </summary>
[PublicAPI]
public readonly record struct AuthorizationGrant(
    IAuthorizationRequest AuthorizationRequest,
    SubjectAuthentication SubjectAuthentication
);
