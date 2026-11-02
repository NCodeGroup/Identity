// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// A value a client registers that a settings change would not permit.
/// </summary>
/// <param name="ClientId">The identifier of the client that registers the value.</param>
/// <param name="Violation">The <see cref="ClientCoherenceViolation"/> the change introduces.</param>
// See ADR-1206.
internal readonly record struct ClientCoherenceFinding(
    string ClientId,
    ClientCoherenceViolation Violation
);
