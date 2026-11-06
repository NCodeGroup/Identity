// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a page of the BFF host that a browser reaches by top-level navigation, so a BFF session authenticates it on
/// <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c> and <c>TRACE</c> without a token and on a form post that passes Fetch
/// Metadata and carries a valid antiforgery token, and a challenge redirects to the BFF login.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffPageAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffPageAttribute"/> class.
    /// </summary>
    public BffPageAttribute()
    {
        AuthenticationSchemes = BffDefaults.AuthenticationScheme;
    }
}
