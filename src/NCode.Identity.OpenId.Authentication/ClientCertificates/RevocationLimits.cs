// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Contains the limits that every source of revocation information applies to what it retrieves.
/// </summary>
internal static class RevocationLimits
{
    /// <summary>
    /// Gets how far the <c>thisUpdate</c> of revocation information may lie in the future, and how old information
    /// without a <c>nextUpdate</c> may be.
    /// </summary>
    public static TimeSpan ClockSkew { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The deepest nested check, such as the path of a signer or a delegated responder, that is still made.
    /// </summary>
    public const int MaxNestedDepth = 2;
}
