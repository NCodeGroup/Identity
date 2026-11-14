// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Options for one remote API that the BFF forwards a local path prefix to.
/// </summary>
[PublicAPI]
public sealed class BffRemoteApiOptions
{
    /// <summary>
    /// Gets or sets the local path prefix that the BFF forwards, such as <c>/api/orders</c>. It starts with <c>/</c>,
    /// does not end with <c>/</c>, and must not overlap another remote API or the BFF's own endpoints.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute base URL that the BFF forwards to, such as <c>https://orders.example.com/</c>. The
    /// rest of the request path after <see cref="Path"/> is appended to it. It must use <c>https</c> unless
    /// <c>LocalDevelopment:Enabled</c> is true, and has no user info, query or fragment.
    /// </summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the token requirement that obtains the outbound token. The default is
    /// <see cref="BffTokenRequirementNames.User"/>.
    /// </summary>
    public string TokenRequirement { get; set; } = BffTokenRequirementNames.User;

    /// <summary>
    /// Gets or sets the resource indicator that the token is requested for, or <c>null</c> for the default.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the space-separated scopes that the token is requested for, or <c>null</c> for the default.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the HTTP methods that the remote API accepts. A request with another method is answered with
    /// <c>405</c>. An empty list accepts every method.
    /// </summary>
    public List<string> AllowedMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets how long the forwarder waits for upstream activity — the response headers, or the next read or
    /// write of a body — before it gives up. The default is 100 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
