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

using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.Server;

/// <summary>
/// Checks the password of a local account that a configuration seeder is about to create, so a seeder stores no
/// password that the tenant's password policy refuses.
/// </summary>
// See ADR-0236.
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
        var serverSettings = await EffectiveSettingsResolver.ResolveServerAsync(cancellationToken);
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
