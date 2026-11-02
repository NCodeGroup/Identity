// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Used to provide additional information about the Claim being requested.
/// </summary>
[PublicAPI]
public interface IRequestClaim
{
    /// <summary>
    /// Gets a value indicating whether the Claim being requested is an Essential or Voluntary Claim.
    /// </summary>
    bool? Essential { get; }

    /// <summary>
    /// Gets a value that should be returned when the Claim is being requested.
    /// </summary>
    string? Value { get; }

    /// <summary>
    /// Gets a set of values that should be returned when the Claim is being requested.
    /// </summary>
    IEnumerable<string>? Values { get; }
}
