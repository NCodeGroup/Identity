// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// A <see cref="ISecurityProfileRequirement"/> evaluated after a client authenticates with a signed assertion. The
/// active profile's requirements of this phase are invoked once the assertion has been validated; an implementation
/// throws an <c>OpenId</c> exception to reject the authentication.
/// </summary>
[PublicAPI]
public interface IClientAuthenticationProfileRequirement
    : ISecurityProfileRequirement<ClientAuthenticationProfileContext>;
