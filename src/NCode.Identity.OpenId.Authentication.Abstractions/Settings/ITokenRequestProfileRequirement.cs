// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// A <see cref="ISecurityProfileRequirement"/> evaluated while processing a token request. The active profile's
/// requirements of this phase are invoked at the token endpoint (after the sender-constraining confirmation is
/// computed); an implementation throws an <c>OpenId</c> exception to reject the request.
/// </summary>
[PublicAPI]
public interface ITokenRequestProfileRequirement
    : ISecurityProfileRequirement<TokenRequestProfileContext>;
