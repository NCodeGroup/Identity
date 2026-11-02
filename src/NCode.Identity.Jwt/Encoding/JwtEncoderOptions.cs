// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Encoding;

/// <summary>
/// Contains configurable options for <see cref="DefaultJwtEncoder"/>.
/// </summary>
[PublicAPI]
public sealed class JwtEncoderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether generated tokens will have default values for the 'iat', 'nbf', and 'exp' claims if not specified.
    /// </summary>
    public bool EnsureTokenLifetime { get; set; } = true;

    /// <summary>
    /// Gets the default lifetime for generated tokens.
    /// </summary>
    public TimeSpan DefaultTokenLifetime { get; } = TimeSpan.FromMinutes(60);
}
