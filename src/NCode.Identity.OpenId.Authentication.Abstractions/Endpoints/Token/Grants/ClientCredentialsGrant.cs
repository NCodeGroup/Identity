// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents a client credentials grant.
/// </summary>
[PublicAPI]
public readonly record struct ClientCredentialsGrant(OpenIdConfidentialClient ConfidentialClient);
