// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Contains the options that bound every call the server makes to a caller-supplied URL: how long one attempt and the
/// whole call may take, how often a failed attempt is retried, and how long a failed fetch of a shared document is
/// remembered.
/// </summary>
// See ADR-1315.
public sealed class OutboundHttpOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "OutboundHttp";

    /// <summary>
    /// Gets or sets the time one attempt of a call may take, the response headers included. An attempt that takes
    /// longer is cancelled and may be retried. The default is ten seconds. It is at least ten milliseconds and at
    /// most one day, and is not greater than <see cref="TotalTimeout"/>.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the time a whole call may take, every retry and the delay between them included. A call that takes
    /// longer is cancelled. The default is thirty seconds. It is at least ten milliseconds and at most one day.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how many times a failed attempt of a call is retried; zero turns the retries off. Only a safe
    /// request method is retried, and never after a connection or name-resolution error. The default is two. It is
    /// between zero and ten.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the base delay before a retry. The delay grows exponentially with each retry and is jittered. The
    /// default is one second. It is between zero and one day.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets how long a failed fetch of a document that many requests share — a client's <c>jwks_uri</c> key
    /// set — is remembered. During that time the document is not fetched again and the requests that need it go
    /// without it. The default is one minute. It is greater than zero and at most one day.
    /// </summary>
    public TimeSpan FailureCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
