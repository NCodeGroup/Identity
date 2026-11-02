// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known reasons that resolving an access token failed.
/// </summary>
/// <remarks>
/// The reasons are an open vocabulary and are compared ordinally.
/// </remarks>
[PublicAPI]
public static class BffTokenFailureReasons
{
    /// <summary>
    /// There is no session.
    /// </summary>
    public const string SessionRequired = "session_required";

    /// <summary>
    /// The refresh token was rejected or is missing.
    /// </summary>
    public const string RefreshFailed = "refresh_failed";

    /// <summary>
    /// Another refresh did not finish within the lease duration.
    /// </summary>
    public const string RefreshWaitTimeout = "refresh_wait_timeout";

    /// <summary>
    /// The provider answered the refresh with an error.
    /// </summary>
    public const string ProviderError = "provider_error";

    /// <summary>
    /// The provider could not be reached.
    /// </summary>
    public const string ProviderUnavailable = "provider_unavailable";
}
