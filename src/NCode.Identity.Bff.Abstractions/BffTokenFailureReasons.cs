#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
