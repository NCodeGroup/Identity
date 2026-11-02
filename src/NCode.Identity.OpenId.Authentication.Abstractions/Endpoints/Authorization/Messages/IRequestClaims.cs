// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides the ability for the authorization request to enable requesting individual <c>Claims</c> and specifying
/// parameters that apply to the requested <c>Claims</c>.
/// </summary>
[PublicAPI]
public interface IRequestClaims
{
    /// <summary>
    /// Gets the information that specifies which specific Claims should be returned from the UserInfo endpoint.
    /// If present, the listed Claims are being requested to be added to any Claims that are being requested using
    /// the <c>scope</c> parameter. If not present, the Claims being requested from the UserInfo endpoint are only
    /// those requested using the <c>scope</c> parameter.
    /// </summary>
    IReadOnlyDictionary<string, IRequestClaim?>? UserInfo { get; }

    /// <summary>
    /// Gets the information that specifies which specific Claims should be returned in the ID Token. If present,
    /// the listed Claims are being requested to be added to the default Claims in the ID Token. If not present,
    /// the default ID Token Claims are requested, as per the ID Token specification and additional per-flow ID
    /// Token requirements.
    /// </summary>
    IReadOnlyDictionary<string, IRequestClaim?>? IdToken { get; }
}
