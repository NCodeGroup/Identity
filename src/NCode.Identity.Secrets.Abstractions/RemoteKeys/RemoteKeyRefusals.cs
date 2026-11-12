// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides the reasons for which a request for a key that a key management service holds is refused, worded once, so
/// a consumer that refuses the request with an exception and one that answers it with an error state it in the same
/// words.
/// </summary>
// See ADR-1474.
[PublicAPI]
public static class RemoteKeyRefusals
{
    /// <summary>
    /// Gets the reason a key cannot be created in a remote key provider whose descriptor creates no keys.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <returns>The reason, naming the provider.</returns>
    public static string CreatesNoKeys(string providerName) =>
        $"The remote key provider '{providerName}' creates no keys: it has no key-creation location.";
}
