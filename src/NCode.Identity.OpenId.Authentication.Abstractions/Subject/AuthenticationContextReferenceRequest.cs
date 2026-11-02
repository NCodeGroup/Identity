// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Contains the inputs used to resolve the <c>acr</c> (Authentication Context Class Reference) value the authorization
/// server asserts for an authentication event.
/// </summary>
[PublicAPI]
public readonly record struct AuthenticationContextReferenceRequest
{
    /// <summary>
    /// Gets the authentication method references (the <c>amr</c> claim values) that were satisfied, such as
    /// <see cref="OpenIdProtocol.AuthenticationMethods.Password"/>.
    /// </summary>
    public required IReadOnlyCollection<string> AuthenticationMethods { get; init; }

    /// <summary>
    /// Gets the identifier of the identity provider (the <c>idp</c> claim value) that authenticated the end-user, or
    /// <see langword="null"/> when the OP itself authenticated the end-user.
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    /// Gets the effective settings governing the authentication, used to honor configured <c>acr</c> policy.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }
}
