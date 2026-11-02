// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes the outcome of resolving a subject's recorded consent against the scopes a request is asking for, used by
/// the authorization endpoint to decide whether the interactive consent surface must be shown and which scopes are
/// actually issued (OIDC down-scoping).
/// </summary>
/// <param name="IsDecided">Whether the subject has already decided on every requested scope (the requested scopes are
/// all contained in the recorded "decided" set), so the consent surface can be skipped.</param>
/// <param name="GrantedScopes">The subset of the requested scopes the subject has granted (allowed), in requested
/// order. These are the scopes that are actually issued when consent is satisfied.</param>
[PublicAPI]
public readonly record struct ConsentResolution(
    bool IsDecided,
    IReadOnlyList<string> GrantedScopes
);
