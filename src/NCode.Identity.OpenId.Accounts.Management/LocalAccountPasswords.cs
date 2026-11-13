// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Judges the passwords set on local accounts against the owning tenant's password policy and encodes them for the
/// store without leaving the plaintext on the GC heap.
/// </summary>
internal sealed class LocalAccountPasswords(
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IPasswordPolicy passwordPolicy
)
{
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IPasswordPolicy PasswordPolicy { get; } = passwordPolicy;

    // Judges a password an administrator sets by the addressed tenant's effective settings, resolved from its stored
    // document so the tenant need not be served. A document that cannot be honored is answered 409 by the management
    // route group. See ADR-1236 and ADR-1245.
    public async ValueTask<ManagementError?> ValidateAsync(
        IStoreManager storeManager,
        string tenantId,
        PersistedLocalAccount account,
        string password,
        CancellationToken cancellationToken
    )
    {
        var tenant = await storeManager
            .GetStore<ITenantStore>()
            .GetOrDefaultAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return new ManagementError
            {
                StatusCode = StatusCodes.Status404NotFound,
                Detail = "The tenant was not found.",
            };
        }

        var teamSettings = await EffectiveSettingsResolver.ResolveTeamAsync(
            tenant.TeamId,
            cancellationToken
        );
        var tenantSettings = EffectiveSettingsResolver.ResolveStored(
            teamSettings,
            OpenIdSettingScopes.Tenant,
            tenantId,
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
        if (!result.IsRejected)
        {
            return null;
        }

        return new ManagementError
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Detail = result.ErrorDescription,
        };
    }

    // Encodes the password into a pinned, zeroed-on-return buffer so the plaintext never lingers on the GC heap.
    public static async ValueTask WithPasswordBytesAsync(
        string password,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> useAsync,
        CancellationToken cancellationToken
    )
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = BufferFactory.Rent(byteCount, isSensitive: true, out Memory<byte> buffer);
        var written = SecureEncoding.UTF8.GetBytes(password, buffer.Span);
        await useAsync(buffer[..written], cancellationToken);
    }
}
