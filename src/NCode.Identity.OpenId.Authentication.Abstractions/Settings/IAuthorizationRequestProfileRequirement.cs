// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// A <see cref="ISecurityProfileRequirement"/> evaluated while validating an authorization request. The active
/// profile's requirements of this phase are invoked at the authorization endpoint; an implementation throws an
/// <c>OpenId</c> exception to reject the request.
/// </summary>
[PublicAPI]
public interface IAuthorizationRequestProfileRequirement
    : ISecurityProfileRequirement<AuthorizationRequestProfileContext>;
