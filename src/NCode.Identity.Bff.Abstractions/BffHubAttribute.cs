// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.Bff;

/// <summary>
/// Marks a SignalR hub of the BFF host, so a BFF session authenticates its negotiate and connection requests when Fetch
/// Metadata shows no cross-site request and the <c>Origin</c>, when sent, is the frontend's public origin or a trusted
/// origin, and a challenge answers <c>401</c>.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffHubAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffHubAttribute"/> class.
    /// </summary>
    public BffHubAttribute()
    {
        AuthenticationSchemes = BffDefaults.AuthenticationScheme;
    }
}
