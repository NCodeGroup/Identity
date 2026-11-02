// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a local BFF API, so the anti-forgery header, Fetch Metadata and a BFF session are required,
/// and a challenge answers <c>401</c>.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffApiAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffApiAttribute"/> class.
    /// </summary>
    public BffApiAttribute()
    {
        AuthenticationSchemes = BffDefaults.AuthenticationScheme;
    }
}
