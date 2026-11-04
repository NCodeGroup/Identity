// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.Server;

/// <summary>
/// Checks the password of a local account that a configuration seeder is about to create, so a seeder stores no
/// password that the tenant's password policy refuses.
/// </summary>
// See ADR-1236.
internal sealed class SeededLocalAccountValidator(
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IPasswordPolicy passwordPolicy
)
{
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IPasswordPolicy PasswordPolicy { get; } = passwordPolicy;

    /// <summary>
    /// Ensures that the password of a local account a seeder is about to create meets the password policy of the
    /// tenant the account is seeded into.
    /// </summary>
    /// <param name="accountKind">The kind of account the seeder creates, which names the account in the error.</param>
    /// <param name="tenant">The tenant the account is seeded into.</param>
    /// <param name="account">The local account the seeder is about to create.</param>
    /// <param name="password">The password the seeder is about to set.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the password is acceptable.</returns>
    /// <exception cref="InvalidOperationException">The password policy refuses the password.</exception>
    public async ValueTask EnsureCanBeSeededAsync(
        string accountKind,
        PersistedTenant tenant,
        PersistedLocalAccount account,
        string password,
        CancellationToken cancellationToken
    )
    {
        var serverSettings = await EffectiveSettingsResolver.ResolvePlatformAsync(
            cancellationToken
        );
        var tenantSettings = EffectiveSettingsResolver.Resolve(
            serverSettings,
            OpenIdSettingScopes.Tenant,
            tenant.Settings.Value
        );

        var passwordMemory = password.AsMemory();
        var policyContext = new PasswordPolicyContext
        {
            Settings = tenantSettings,
            UserName = account.UserName,
            Email = account.Email,
        };
        var result = await PasswordPolicy.ValidateAsync(
            policyContext,
            passwordMemory,
            cancellationToken
        );
        if (result.IsRejected)
        {
            throw new InvalidOperationException(
                $"The {accountKind} '{account.UserName}' cannot be seeded: {result.ErrorDescription}"
            );
        }
    }
}
