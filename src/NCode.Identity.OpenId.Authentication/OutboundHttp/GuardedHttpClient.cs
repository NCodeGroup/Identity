// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Names the one HTTP client that serves every fetch of, and delivery to, a caller-supplied URL.
/// </summary>
// See ADR-1315.
internal static class GuardedHttpClient
{
    /// <summary>
    /// The name of the guarded HTTP client.
    /// </summary>
    internal const string Name = "NCode.Identity.OpenId.GuardedHttp";
}
