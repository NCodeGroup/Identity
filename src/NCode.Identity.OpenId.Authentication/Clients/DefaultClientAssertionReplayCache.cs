// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default implementation of the <see cref="IClientAssertionReplayCache"/> abstraction over the shared
/// <see cref="IExpiringKeySet"/>, so client-assertion replay (RFC 7523 §3) is detected by every replica that shares the
/// set. The set's atomic add-if-absent lets exactly one of any number of concurrent presentations of an assertion
/// succeed.
/// </summary>
internal sealed class DefaultClientAssertionReplayCache(IExpiringKeySet keySet)
    : IClientAssertionReplayCache
{
    private const string Purpose = "client_assertion_jti";

    private IExpiringKeySet KeySet { get; } = keySet;

    /// <inheritdoc />
    public ValueTask<bool> TryRecordAsync(
        OpenIdContext openIdContext,
        string clientId,
        string jti,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var key = ExpiringKeys.Create(Purpose, openIdContext.Tenant.TenantId, clientId, jti);
        return KeySet.TryAddAsync(key, expiresWhen, cancellationToken);
    }
}
