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

using System.Text;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.Passwords;

/// <summary>
/// Provides the default <see cref="IPasswordAttemptLimiter"/>. It counts the failed attempts of an account in the
/// shared <see cref="IExpiringCounterSet"/>, keyed by the tenant and the folded user name. The first failure starts a
/// window of <c>password_failed_attempts_window</c>; once <c>password_failed_attempts_limit</c> failures are counted,
/// every password is refused until the window ends. A counter store that throws propagates the exception, so the limit
/// fails closed.
/// </summary>
// See ADR-0237.
internal class DefaultPasswordAttemptLimiter(
    IExpiringCounterSet counterSet,
    TimeProvider timeProvider,
    ILogger<DefaultPasswordAttemptLimiter> logger
) : IPasswordAttemptLimiter
{
    internal const string Purpose = "password-attempts";

    private IExpiringCounterSet CounterSet { get; } = counterSet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultPasswordAttemptLimiter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<bool> IsLimitReachedAsync(
        OpenIdContext openIdContext,
        string userName,
        CancellationToken cancellationToken
    )
    {
        var tenant = openIdContext.Tenant;
        var limit = tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.PasswordFailedAttemptsLimit
        );

        var key = CreateKey(tenant.TenantId, userName);
        var count = await CounterSet.GetAsync(key, cancellationToken);
        if (count < limit)
            return false;

        Logger.PasswordAttemptLimitReached(tenant.TenantId, limit);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask RecordFailureAsync(
        OpenIdContext openIdContext,
        string userName,
        CancellationToken cancellationToken
    )
    {
        var tenant = openIdContext.Tenant;
        var window = tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.PasswordFailedAttemptsWindow
        );

        var key = CreateKey(tenant.TenantId, userName);
        var now = TimeProvider.GetUtcNow();
        var expiresWhen = now + window;
        await CounterSet.IncrementAsync(key, expiresWhen, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask ResetAsync(
        string tenantId,
        string userName,
        CancellationToken cancellationToken
    )
    {
        var key = CreateKey(tenantId, userName);
        await CounterSet.RemoveAsync(key, cancellationToken);
    }

    internal static string CreateKey(string tenantId, string userName)
    {
        var normalizedUserName = NormalizeUserName(userName);
        return ExpiringKeys.Create(Purpose, tenantId, normalizedUserName);
    }

    // The stores compare names differently (EF lower-cases; ASP.NET Core Identity composes and upper-cases), and
    // folding through both gives every spelling a store treats as the same name one count. The strings here come from
    // form or JSON readers, which never yield a lone surrogate, so normalization cannot fail.
    private static string NormalizeUserName(string userName) =>
        userName.Normalize(NormalizationForm.FormKC).ToUpperInvariant().ToLowerInvariant();
}
