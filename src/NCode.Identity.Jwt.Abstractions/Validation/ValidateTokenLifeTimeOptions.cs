// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Provides the ability to configure the behavior when validating the lifetime of a JSON Web Token (JWT).
/// </summary>
[PublicAPI]
public sealed class ValidateTokenLifeTimeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the <c>exp</c> claim is required.
    /// The default is <c>true</c>.
    /// </summary>
    public bool RequireExpirationTime { get; set; } = true;

    /// <summary>
    /// Gets or sets the longest lifetime the token may have, or <see langword="null"/> (the default) for no maximum.
    /// See <see cref="TokenLifetimeRequirement.MaxLifetime"/> for how the lifetime is measured.
    /// </summary>
    public TimeSpan? MaxLifetime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>nbf</c> claim is required.
    /// The default is <c>false</c>.
    /// </summary>
    public bool RequireNotBefore { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>iat</c> claim is required.
    /// The default is <c>false</c>.
    /// </summary>
    public bool RequireIssuedAt { get; set; }
}
