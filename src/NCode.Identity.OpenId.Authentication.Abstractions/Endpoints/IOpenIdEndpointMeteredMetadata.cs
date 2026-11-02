// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents whether a request to the associated endpoint is the tenant's metered usage: counted against the tenant's
/// daily request quota and its overage, and recorded in the tenant's usage ledger. An endpoint that serves public,
/// unauthenticated metadata — such as the discovery document or the JSON Web Key Set — is not metered, so anonymous
/// traffic cannot spend a tenant's quota or bill its overage; the per-minute rate limits still apply to it. An endpoint
/// without this metadata is metered.
/// </summary>
// See ADR-1198 and ADR-1199.
[PublicAPI]
public interface IOpenIdEndpointMeteredMetadata
{
    /// <summary>
    /// Gets a value indicating whether requests to the associated endpoint are metered.
    /// </summary>
    bool IsMetered { get; }
}
