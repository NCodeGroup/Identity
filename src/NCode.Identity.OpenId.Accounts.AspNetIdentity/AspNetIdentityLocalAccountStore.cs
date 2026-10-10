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

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Adapts an ASP.NET Core Identity <see cref="UserManager{TUser}"/> to the backing-store-agnostic
/// <see cref="ILocalAccountStore"/> seam, so a host already running ASP.NET Core Identity exposes its users as local
/// accounts (enabling the resource-owner password grant and the local-account management endpoints) with no change to
/// the OpenID runtime. Credential hashing, security stamps, and concurrency are owned by the <see cref="UserManager{TUser}"/>.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity users are global (tenant-independent), so <see cref="PersistedLocalAccount.TenantId"/> is mapped
/// as empty; the resource-owner password grant authenticates against the request's tenant, not the account's. Each
/// operation resolves the scoped <see cref="UserManager{TUser}"/> from a fresh dependency-injection scope, so this
/// adapter holds no scoped state and its writes commit immediately (they do not enlist in the OpenID unit of work).
/// </remarks>
/// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
internal sealed class AspNetIdentityLocalAccountStore<TUser>(
    IServiceScopeFactory serviceScopeFactory,
    IStoreProvider storeProvider
) : ILocalAccountStore
    where TUser : IdentityUser, new()
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedLocalAccount account,
        ReadOnlyMemory<byte>? password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        // The id is assigned by the orchestrating create handler (it is also the self-issued identity's subject), so it
        // must be carried onto the Identity user rather than letting the store generate its own.
        var user = new TUser
        {
            Id = account.LocalAccountId,
            UserName = account.UserName,
            Email = account.Email,
            EmailConfirmed = account.EmailVerified,
        };

        var result = password is { } value
            ? await userManager.CreateAsync(user, DecodePassword(value))
            : await userManager.CreateAsync(user);
        ThrowIfFailed(result, "create the local account");

        foreach (var claim in account.Claims)
        {
            ThrowIfFailed(
                await userManager.AddClaimAsync(user, new Claim(claim.Type, claim.Value)),
                "add a local-account claim"
            );
        }

        if (!account.IsEnabled)
        {
            await DisableAsync(userManager, user);
        }
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await GetRequiredUserAsync(userManager, account.LocalAccountId);

        // SetUserNameAsync/SetEmailAsync also refresh the normalized lookup columns; setting the properties directly
        // would leave those stale. SetEmailAsync resets the confirmed flag, so the confirmation is applied after it.
        ThrowIfFailed(
            await userManager.SetUserNameAsync(user, account.UserName),
            "update the username"
        );
        ThrowIfFailed(await userManager.SetEmailAsync(user, account.Email), "update the email");
        user.EmailConfirmed = account.EmailVerified;

        if (account.IsEnabled)
        {
            await EnableAsync(userManager, user);
        }
        else
        {
            await DisableAsync(userManager, user);
        }

        ThrowIfFailed(await userManager.UpdateAsync(user), "update the local account");
    }

    /// <inheritdoc />
    public async ValueTask SetPasswordAsync(
        string localAccountId,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await GetRequiredUserAsync(userManager, localAccountId);

        // Resetting through a generated token rotates the security stamp, invalidating prior sessions and tokens.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        ThrowIfFailed(
            await userManager.ResetPasswordAsync(user, token, DecodePassword(password)),
            "reset the local-account password"
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> VerifyCredentialAsync(
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return null;
        }

        var isEnabled =
            !userManager.SupportsUserLockout || !await userManager.IsLockedOutAsync(user);
        if (!isEnabled)
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, DecodePassword(password)))
        {
            return null;
        }

        return await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByIdAsync(localAccountId);
        return user is null ? null : await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByUserNameOrDefaultAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByNameAsync(userName);
        return user is null ? null : await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedLocalAccount>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        if (!userManager.SupportsQueryableUsers)
        {
            throw new NotSupportedException(
                "The configured ASP.NET Core Identity store does not support querying users."
            );
        }

        var query = userManager.Users;
        if (!string.IsNullOrEmpty(cursor))
        {
            // Keyset by id; the untyped comparison is translated to a SQL range predicate by the provider (the database
            // collation, not an in-memory ordinal comparison, orders the page), so CA1309 does not apply here.
#pragma warning disable CA1309
            query = query.Where(user => string.Compare(user.Id, cursor) > 0);
#pragma warning restore CA1309
        }

        // Fetch one extra row to determine whether a further page exists.
        var users = query.OrderBy(user => user.Id).Take(limit + 1).ToList();

        var hasMore = users.Count > limit;
        var items = new List<PersistedLocalAccount>(Math.Min(users.Count, limit));
        for (var index = 0; index < users.Count && index < limit; index++)
        {
            items.Add(await MapAsync(userManager, users[index]));
        }

        var nextCursor = hasMore ? items[^1].LocalAccountId : null;
        return new PagedResult<PersistedLocalAccount> { Items = items, NextCursor = nextCursor };
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string localAccountId, CancellationToken cancellationToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByIdAsync(localAccountId);
        if (user is null)
        {
            return;
        }

        ThrowIfFailed(await userManager.DeleteAsync(user), "delete the local account");
    }

    /// <inheritdoc />
    public async ValueTask ReplaceClaimsAsync(
        string localAccountId,
        IReadOnlyList<PersistedLocalAccountClaim> claims,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await GetRequiredUserAsync(userManager, localAccountId);

        var existing = await userManager.GetClaimsAsync(user);
        if (existing.Count > 0)
        {
            ThrowIfFailed(
                await userManager.RemoveClaimsAsync(user, existing),
                "remove the existing local-account claims"
            );
        }

        if (claims.Count > 0)
        {
            ThrowIfFailed(
                await userManager.AddClaimsAsync(
                    user,
                    claims.Select(claim => new Claim(claim.Type, claim.Value))
                ),
                "add the local-account claims"
            );
        }
    }

    private static async ValueTask<PersistedLocalAccount> MapAsync(
        UserManager<TUser> userManager,
        TUser user
    )
    {
        var claims = await userManager.GetClaimsAsync(user);
        var isEnabled =
            !userManager.SupportsUserLockout || !await userManager.IsLockedOutAsync(user);

        return new PersistedLocalAccount
        {
            // ASP.NET Core Identity users are global; the grant authenticates against the request's tenant.
            TenantId = string.Empty,
            LocalAccountId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            EmailVerified = user.EmailConfirmed,
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            IsEnabled = isEnabled,
            Claims = claims
                .Select(claim => new PersistedLocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = user.ConcurrencyStamp ?? string.Empty,
        };
    }

    private static async ValueTask EnableAsync(UserManager<TUser> userManager, TUser user)
    {
        if (userManager.SupportsUserLockout)
        {
            ThrowIfFailed(
                await userManager.SetLockoutEndDateAsync(user, lockoutEnd: null),
                "enable the local account"
            );
        }
    }

    private static async ValueTask DisableAsync(UserManager<TUser> userManager, TUser user)
    {
        if (!userManager.SupportsUserLockout)
        {
            throw new NotSupportedException(
                "The configured ASP.NET Core Identity store does not support lockout, which is required to disable a local account."
            );
        }

        ThrowIfFailed(
            await userManager.SetLockoutEnabledAsync(user, enabled: true),
            "enable lockout for the local account"
        );
        ThrowIfFailed(
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue),
            "disable the local account"
        );
    }

    private static async ValueTask<TUser> GetRequiredUserAsync(
        UserManager<TUser> userManager,
        string localAccountId
    ) =>
        await userManager.FindByIdAsync(localAccountId)
        ?? throw new InvalidOperationException(
            $"A local account with '{localAccountId}' was not found."
        );

    // ASP.NET Core Identity's credential API is string-based, so the credential is decoded to a string only here.
    private static string DecodePassword(ReadOnlyMemory<byte> password) =>
        SecureEncoding.UTF8.GetString(password.Span);

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {action}: {errors}");
    }
}
